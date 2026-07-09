using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Web.Services;

public sealed class BackgroundDownloadQueue : BackgroundService, IBackgroundDownloadQueue
{
    private readonly Channel<StartDownloadRequest> _queue = Channel.CreateBounded<StartDownloadRequest>(new BoundedChannelOptions(2)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobRepository _jobRepository;

    public BackgroundDownloadQueue(IServiceScopeFactory scopeFactory, IJobRepository jobRepository)
    {
        _scopeFactory = scopeFactory;
        _jobRepository = jobRepository;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<IDownloadWorkflow>();
            await workflow.StartAsync(request, new Progress<DownloadProgress>(), stoppingToken);
        }
    }
}
