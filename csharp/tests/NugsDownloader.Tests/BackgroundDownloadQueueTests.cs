using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Web.Options;
using NugsDownloader.Web.Services;
using Xunit;

namespace NugsDownloader.Tests;

public sealed class BackgroundDownloadQueueTests
{
    [Fact]
    public async Task Worker_RecoversInterruptedLease_AndRunsReplayWithoutReprocessingCompletedJobs()
    {
        var repository = new MemoryJobs();
        var interruptedId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        await repository.SaveAsync(Job(interruptedId, DownloadJobStatus.Running, "old-worker"), CancellationToken.None);
        await repository.SaveAsync(Job(completedId, DownloadJobStatus.Completed, "old-worker"), CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, interruptedId);
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await queue.StopAsync(CancellationToken.None);

        var recovered = await repository.GetAsync(interruptedId, CancellationToken.None);
        var completed = await repository.GetAsync(completedId, CancellationToken.None);
        Assert.Equal(DownloadJobStatus.Completed, recovered!.Status);
        Assert.Null(recovered.LeaseOwnerId);
        Assert.Equal(DownloadJobStatus.Completed, completed!.Status);
        Assert.Equal(1, workflow.StartCount);
    }

    [Fact]
    public async Task Worker_RespectsConfiguredConcurrency()
    {
        var repository = new MemoryJobs();
        var workflow = new RecordingWorkflow(repository, null, TimeSpan.FromMilliseconds(120));
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));
        await queue.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(Request(), CancellationToken.None);
        await queue.EnqueueAsync(Request(), CancellationToken.None);
        await workflow.Completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal(2, workflow.StartCount);
        Assert.Equal(1, workflow.MaxConcurrent);
    }

    [Fact]
    public async Task Startup_ReplaysMoreDueJobsThanChannelCapacityWithoutDeadlocking()
    {
        var repository = new MemoryJobs();
        for (var index = 0; index < 4; index++)
        {
            await repository.SaveAsync(Job(Guid.NewGuid(), DownloadJobStatus.Running, "old-worker"), CancellationToken.None);
        }

        var workflow = new RecordingWorkflow(repository, null, replayAll: true);
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await workflow.AllCompleted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal(4, workflow.StartCount);
    }

    [Fact]
    public async Task Startup_DoesNotReplayJobWithUnexpiredLease()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        var leaseExpiry = DateTimeOffset.UtcNow.AddMinutes(5);
        await repository.SaveAsync(Job(jobId, DownloadJobStatus.Running, "active-worker", leaseExpiry), CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, null, replayAll: true);
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await queue.StopAsync(CancellationToken.None);

        var job = await repository.GetAsync(jobId, CancellationToken.None);
        Assert.Equal(0, workflow.StartCount);
        Assert.Equal("active-worker", job!.LeaseOwnerId);
        Assert.Equal(leaseExpiry, job.LeaseExpiresAt);
        Assert.Equal(DownloadJobStatus.Running, job.Status);
    }

    [Fact]
    public async Task Scanner_RecoversJobWhenLeaseExpiresAfterStartup()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        await repository.SaveAsync(
            Job(jobId, DownloadJobStatus.Running, "departed-worker", DateTimeOffset.UtcNow.AddMilliseconds(300)),
            CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, jobId);
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await queue.StopAsync(CancellationToken.None);

        var recovered = await repository.GetAsync(jobId, CancellationToken.None);
        Assert.Equal(DownloadJobStatus.Completed, recovered!.Status);
        Assert.Equal(1, workflow.StartCount);
    }

    [Fact]
    public async Task Startup_RecoversClaimedReadyJobWithExpiredLeaseAndNoDueTimestamp()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        var claimed = Job(jobId, DownloadJobStatus.Ready, "departed-worker") with { NextAttemptAt = null };
        await repository.SaveAsync(claimed, CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, jobId);
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await queue.StopAsync(CancellationToken.None);

        var recovered = await repository.GetAsync(jobId, CancellationToken.None);
        Assert.Equal(DownloadJobStatus.Completed, recovered!.Status);
        Assert.Equal(1, workflow.StartCount);
    }

    [Fact]
    public async Task Worker_RenewsLeaseWhileDownloadIsActive()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        var workflow = new RecordingWorkflow(repository, jobId, TimeSpan.FromSeconds(3));
        await using var provider = new ServiceCollection()
            .AddScoped<IDownloadWorkflow>(_ => workflow)
            .BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 2, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(Request(jobId), CancellationToken.None);
        await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var initialExpiry = (await repository.GetAsync(jobId, CancellationToken.None))!.LeaseExpiresAt;
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        var renewedExpiry = (await repository.GetAsync(jobId, CancellationToken.None))!.LeaseExpiresAt;
        await workflow.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await queue.StopAsync(CancellationToken.None);

        Assert.NotNull(initialExpiry);
        Assert.True(renewedExpiry > initialExpiry);
    }

    [Fact]
    public async Task ManualRetry_RejectsJobOwnedByActiveWorker()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        await repository.SaveAsync(Job(jobId, DownloadJobStatus.Running, "active-worker", DateTimeOffset.UtcNow.AddMinutes(5)), CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, jobId);
        await using var provider = new ServiceCollection().AddScoped<IDownloadWorkflow>(_ => workflow).BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        var result = await queue.RetryAsync(jobId, CancellationToken.None);

        Assert.Equal("LeaseActive", result.Status);
        Assert.Equal(0, workflow.StartCount);
        Assert.Equal("active-worker", (await repository.GetAsync(jobId, CancellationToken.None))?.LeaseOwnerId);
    }

    [Fact]
    public async Task ManualRetry_ClearsAutomaticBackoffAndRunsImmediately()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        await repository.SaveAsync(Job(jobId, DownloadJobStatus.Ready, null) with { NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1) }, CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, jobId);
        await using var provider = new ServiceCollection().AddScoped<IDownloadWorkflow>(_ => workflow).BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        var result = await queue.RetryAsync(jobId, CancellationToken.None);
        await queue.StartAsync(CancellationToken.None);
        await workflow.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal("RetryQueued", result.Status);
        Assert.Equal(1, workflow.StartCount);
        Assert.Null((await repository.GetAsync(jobId, CancellationToken.None))?.NextAttemptAt);
    }

    [Fact]
    public async Task FinalFailure_ReleasesLeaseSoManualRetryCanRunImmediately()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        await repository.SaveAsync(Job(jobId, DownloadJobStatus.Ready, null) with { NextAttemptAt = null }, CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, jobId, fail: true);
        await using var provider = new ServiceCollection().AddScoped<IDownloadWorkflow>(_ => workflow).BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1, MaxRetries = 0 }));

        await queue.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(Request(jobId), CancellationToken.None);
        await workflow.Completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var retry = await queue.RetryAsync(jobId, CancellationToken.None);
        await queue.StopAsync(CancellationToken.None);

        Assert.Equal("RetryQueued", retry.Status);
        Assert.Null((await repository.GetAsync(jobId, CancellationToken.None))?.LeaseOwnerId);
    }

    [Fact]
    public async Task Scanner_MarksDueJobNeedsAuthenticationWhenReplayCredentialsAreUnavailable()
    {
        var repository = new MemoryJobs();
        var jobId = Guid.NewGuid();
        await repository.SaveAsync(Job(jobId, DownloadJobStatus.Ready, null) with { NextAttemptAt = DateTimeOffset.UtcNow }, CancellationToken.None);
        var workflow = new RecordingWorkflow(repository, replayId: null);
        await using var provider = new ServiceCollection().AddScoped<IDownloadWorkflow>(_ => workflow).BuildServiceProvider();
        var queue = new BackgroundDownloadQueue(provider.GetRequiredService<IServiceScopeFactory>(), repository,
            Options.Create(new BackgroundDownloadQueueOptions { Capacity = 2, Concurrency = 1, LeaseSeconds = 60, RetryDelaySeconds = 1 }));

        await queue.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await queue.StopAsync(CancellationToken.None);

        var job = await repository.GetAsync(jobId, CancellationToken.None);
        Assert.Equal(DownloadJobStatus.Failed, job?.Status);
        Assert.Contains("Re-authenticate", job?.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Null(job?.NextAttemptAt);
    }

    private static StartDownloadRequest Request(Guid? id = null) => new(id, "nugs", new Uri("https://example.com/release"), new Credentials("user", "secret", null, "main"), new DownloadPreferences(null, null, false, false, false, "Downloads", true, true));

    private static DownloadJob Job(Guid id, DownloadJobStatus status, string? owner, DateTimeOffset? leaseExpiry = null) => new(id, "nugs", new Uri("https://example.com/release"), "Demo", status, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, status == DownloadJobStatus.Completed ? DateTimeOffset.UtcNow : null, null, "Downloads", "main", "user", null, owner, DateTimeOffset.UtcNow, leaseExpiry ?? DateTimeOffset.UtcNow.AddMinutes(-1));

    private sealed class RecordingWorkflow(MemoryJobs repository, Guid? replayId, TimeSpan? delay = null, bool replayAll = false, bool fail = false) : IDownloadWorkflow
    {
        private int _active;
        public int StartCount { get; private set; }
        public int MaxConcurrent { get; private set; }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> AllCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<StartDownloadResult> StartAsync(StartDownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            StartCount++;
            var active = Interlocked.Increment(ref _active);
            MaxConcurrent = Math.Max(MaxConcurrent, active);
            Started.TrySetResult(true);
            if (delay is { } pause) await Task.Delay(pause, ct);
            Interlocked.Decrement(ref _active);
            var previous = await repository.GetAsync(request.JobId!.Value, ct);
            if (previous is not null)
            {
                await repository.SaveAsync(previous with
                {
                    Status = fail ? DownloadJobStatus.Failed : DownloadJobStatus.Completed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = fail ? "expected test failure" : null
                }, ct);
            }
            if (fail || StartCount >= 2 || replayId is not null) Completed.TrySetResult(true);
            if (replayAll && StartCount >= 4) AllCompleted.TrySetResult(true);
            return new StartDownloadResult(request.JobId ?? Guid.NewGuid(), "nugs", "Completed", "done");
        }

        public Task<StartDownloadResult> ResumeAsync(Guid jobId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StartDownloadResult> RetryAsync(Guid jobId, CancellationToken ct) => throw new NotSupportedException();
        public Task<StartDownloadRequest?> BuildReplayRequestAsync(Guid jobId, CancellationToken ct) =>
            Task.FromResult(replayAll || jobId == replayId ? Request(jobId) : null);
    }

    private sealed class MemoryJobs : IJobRepository
    {
        private readonly Dictionary<Guid, DownloadJob> _items = new();
        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_items.GetValueOrDefault(id));
        public Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadJob>>(_items.Values.ToArray());
        public Task SaveAsync(DownloadJob job, CancellationToken ct) { _items[job.Id] = job; return Task.CompletedTask; }
    }
}
