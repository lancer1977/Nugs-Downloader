using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Web.Options;

namespace NugsDownloader.Web.Services;

public sealed class BackgroundDownloadQueue : BackgroundService, IBackgroundDownloadQueue
{
    private readonly Channel<StartDownloadRequest> _queue;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningJobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobRepository _jobRepository;
    private readonly BackgroundDownloadQueueOptions _options;
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public BackgroundDownloadQueue(
        IServiceScopeFactory scopeFactory,
        IJobRepository jobRepository,
        IOptions<BackgroundDownloadQueueOptions> options)
    {
        _scopeFactory = scopeFactory;
        _jobRepository = jobRepository;
        _options = Normalize(options.Value);
        _queue = Channel.CreateBounded<StartDownloadRequest>(new BoundedChannelOptions(_options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = _options.Concurrency == 1,
            SingleWriter = false
        });
    }

    public async Task<StartDownloadResult> EnqueueAsync(StartDownloadRequest request, CancellationToken ct)
    {
        var jobId = request.JobId ?? Guid.NewGuid();
        var queuedRequest = request with { JobId = jobId };
        var now = DateTimeOffset.UtcNow;
        await _jobRepository.SaveAsync(new DownloadJob(
            jobId,
            request.ProviderId ?? string.Empty,
            request.SourceUrl,
            request.SourceUrl.ToString(),
            DownloadJobStatus.Pending,
            now,
            null,
            null,
            null,
            request.Preferences.OutputRoot,
            request.Credentials.Label,
            request.Credentials.Username,
            request.Preferences), ct);

        await _queue.Writer.WriteAsync(queuedRequest, ct);
        return new StartDownloadResult(jobId, request.ProviderId ?? string.Empty, "Queued", "Download queued for background processing.");
    }

    public async Task<StartDownloadResult> CancelAsync(Guid jobId, CancellationToken ct)
    {
        if (_runningJobs.TryGetValue(jobId, out var cancellation))
        {
            await cancellation.CancelAsync();
            return new StartDownloadResult(jobId, string.Empty, "CancelRequested", "Cancellation requested for the running download.");
        }

        var job = await _jobRepository.GetAsync(jobId, ct);
        if (job is null)
        {
            return new StartDownloadResult(jobId, string.Empty, "NotFound", "Job not found.");
        }

        if (job.Status is DownloadJobStatus.Pending or DownloadJobStatus.Ready or DownloadJobStatus.Paused)
        {
            await _jobRepository.SaveAsync(job with
            {
                Status = DownloadJobStatus.Cancelled,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorMessage = "Download was cancelled before it started."
            }, ct);
            return new StartDownloadResult(jobId, job.ProviderId, "Cancelled", "Download was cancelled before it started.");
        }

        return new StartDownloadResult(jobId, job.ProviderId, "NotCancellable", $"Job in {job.Status} state cannot be cancelled.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, _options.Concurrency)
            .Select(_ => RunWorkerAsync(stoppingToken))
            .ToArray();

        await RecoverLeasesAndScheduleReadyJobsAsync(stoppingToken);
        var retryScanner = ScanForDueRetriesAsync(stoppingToken);

        await Task.WhenAll(workers.Append(retryScanner));
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var jobId = request.JobId ?? Guid.NewGuid();
            var leased = await TryAcquireLeaseAsync(jobId, stoppingToken);
            if (!leased)
            {
                continue;
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _runningJobs[jobId] = linkedCancellation;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var workflow = scope.ServiceProvider.GetRequiredService<IDownloadWorkflow>();
                await workflow.StartAsync(request with { JobId = jobId }, new Progress<DownloadProgress>(), linkedCancellation.Token);
                await ScheduleFailureRetryAsync(jobId, stoppingToken);
                await ReleaseTerminalLeaseAsync(jobId, stoppingToken);
            }
            finally
            {
                _runningJobs.TryRemove(jobId, out _);
            }
        }
    }

    private async Task RecoverLeasesAndScheduleReadyJobsAsync(CancellationToken ct)
    {
        var jobs = await _jobRepository.ListAsync(ct);
        foreach (var job in jobs.Where(job => job.Status is DownloadJobStatus.Pending or DownloadJobStatus.Discovering or DownloadJobStatus.Running))
        {
            await _jobRepository.SaveAsync(job with
            {
                Status = DownloadJobStatus.Ready,
                CompletedAt = null,
                ErrorMessage = "Job lease was interrupted by a worker restart and is ready to retry.",
                LeaseOwnerId = null,
                LeaseAcquiredAt = null,
                LeaseExpiresAt = null,
                NextAttemptAt = DateTimeOffset.UtcNow
            }, ct);
        }

        await EnqueueDueRetriesAsync(ct);
    }

    private async Task ScanForDueRetriesAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.RetryDelaySeconds)));
        while (await timer.WaitForNextTickAsync(ct))
        {
            await EnqueueDueRetriesAsync(ct);
        }
    }

    private async Task EnqueueDueRetriesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var jobs = await _jobRepository.ListAsync(ct);
        foreach (var job in jobs.Where(job => job.Status == DownloadJobStatus.Ready && job.NextAttemptAt <= now))
        {
            using var scope = _scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IDownloadWorkflow>();
            var replay = await workflow.BuildReplayRequestAsync(job.Id, ct);
            if (replay is null)
            {
                continue;
            }

            await _jobRepository.SaveAsync(job with { NextAttemptAt = null }, ct);
            await _queue.Writer.WriteAsync(replay with { JobId = job.Id }, ct);
        }
    }

    private async Task<bool> TryAcquireLeaseAsync(Guid jobId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await _jobRepository.TryAcquireLeaseAsync(
            jobId,
            _workerId,
            now,
            now.AddSeconds(_options.LeaseSeconds),
            ct);
    }

    private async Task ScheduleFailureRetryAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _jobRepository.GetAsync(jobId, ct);
        if (job is null || job.Status != DownloadJobStatus.Failed || job.RetryCount >= _options.MaxRetries)
        {
            return;
        }

        var retryCount = job.RetryCount + 1;
        var delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds * Math.Pow(2, retryCount - 1));
        await _jobRepository.SaveAsync(job with
        {
            Status = DownloadJobStatus.Ready,
            CompletedAt = null,
            ErrorMessage = $"Retry {retryCount} of {_options.MaxRetries} scheduled after failure: {job.ErrorMessage}",
            RetryCount = retryCount,
            NextAttemptAt = DateTimeOffset.UtcNow.Add(delay),
            LeaseOwnerId = null,
            LeaseAcquiredAt = null,
            LeaseExpiresAt = null
        }, ct);
    }

    private async Task ReleaseTerminalLeaseAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _jobRepository.GetAsync(jobId, ct);
        if (job?.Status is not (DownloadJobStatus.Completed or DownloadJobStatus.Cancelled))
        {
            return;
        }

        await _jobRepository.SaveAsync(job with
        {
            LeaseOwnerId = null,
            LeaseAcquiredAt = null,
            LeaseExpiresAt = null,
            NextAttemptAt = null
        }, ct);
    }

    private static BackgroundDownloadQueueOptions Normalize(BackgroundDownloadQueueOptions options) => new()
    {
        Capacity = Math.Max(1, options.Capacity),
        Concurrency = Math.Max(1, options.Concurrency),
        LeaseSeconds = Math.Max(1, options.LeaseSeconds),
        RetryDelaySeconds = Math.Max(1, options.RetryDelaySeconds),
        MaxRetries = Math.Max(0, options.MaxRetries)
    };
}
