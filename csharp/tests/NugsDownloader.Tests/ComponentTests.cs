using Bunit;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NugsDownloader.Tests;

public class ComponentTests : TestContext
{
    [Fact]
    public void HomePage_RendersProvidersJobsAndState()
    {
        var provider = new FakeProvider();
        Services.AddSingleton<IDownloadWorkflow>(new FakeWorkflow());
        Services.AddSingleton<IProviderCatalog>(new InMemoryProviderCatalog(new[] { provider }));
        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(new[]
        {
            new DownloadJob(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), provider.Id, new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Downloads")
        }));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(new[]
        {
            new FileState(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Downloads/Demo/file.flac", FileKind.Audio, FileStatus.Complete, 0, 0, null, DateTimeOffset.UtcNow)
        }));

        var cut = RenderComponent<Home>();

        Assert.Contains("NugsDownloader", cut.Markup);
        Assert.Contains("Demo", cut.Markup);
        Assert.Contains("Downloads/Demo/file.flac", cut.Markup);
        Assert.Contains(provider.DisplayName, cut.Markup);
    }

    [Fact]
    public async Task HomePage_SubmitFlow_CallsWorkflowAndShowsResult()
    {
        var provider = new FakeProvider();
        var workflow = new RecordingWorkflow();
        Services.AddSingleton<IDownloadWorkflow>(workflow);
        Services.AddSingleton<IProviderCatalog>(new InMemoryProviderCatalog(new[] { provider }));
        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(Array.Empty<DownloadJob>()));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(Array.Empty<FileState>()));

        var cut = RenderComponent<Home>();

        cut.FindAll("input")[0].Change("https://example.com/release/1");
        cut.FindAll("input")[1].Change("alice");
        cut.FindAll("input")[2].Change("secret");
        cut.FindAll("input")[3].Change("Morning Show");

        await cut.InvokeAsync(() => cut.Find("button[type=submit]").Click());

        Assert.NotNull(workflow.Request);
        Assert.Equal(provider.Id, workflow.Request!.ProviderId);
        Assert.Equal("https://example.com/release/1", workflow.Request.SourceUrl.ToString());
        Assert.Contains("Completed: ok", cut.Markup);
    }

    [Fact]
    public void JobDetailsPage_RendersJobAndFiles()
    {
        var jobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(new[]
        {
            new DownloadJob(jobId, "nugs", new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Downloads")
        }));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(new[]
        {
            new FileState(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), jobId, "Downloads/Demo/file.flac", FileKind.Audio, FileStatus.Complete, 0, 0, null, DateTimeOffset.UtcNow)
        }));

        var cut = RenderComponent<JobDetails>(parameters => parameters.Add(p => p.JobId, jobId));

        Assert.Contains("Job Details", cut.Markup);
        Assert.Contains("Demo", cut.Markup);
        Assert.Contains("Downloads/Demo/file.flac", cut.Markup);
    }

    private sealed class FakeWorkflow : IDownloadWorkflow
    {
        public Task<StartDownloadResult> StartAsync(StartDownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct) =>
            Task.FromResult(new StartDownloadResult(Guid.NewGuid(), request.ProviderId ?? "nugs", "Completed", "ok"));
    }

    private sealed class RecordingWorkflow : IDownloadWorkflow
    {
        public StartDownloadRequest? Request { get; private set; }

        public Task<StartDownloadResult> StartAsync(StartDownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new StartDownloadResult(Guid.NewGuid(), request.ProviderId ?? "nugs", "Completed", "ok"));
        }
    }

    private sealed class FakeProvider : IMediaProvider
    {
        public string Id => "nugs";
        public string DisplayName => "Nugs";
        public ProviderCapabilities Capabilities { get; } = new(true, true, true, true, true, true, Array.Empty<string>(), Array.Empty<string>());
        public bool CanHandle(Uri uri) => true;
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(true, Id, "secret", DisplayName, null, null));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Demo", null, Array.Empty<MediaItem>(), false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<FileState>(), null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class MemoryJobRepository : IJobRepository
    {
        private readonly IReadOnlyList<DownloadJob> _jobs;
        public MemoryJobRepository(IReadOnlyList<DownloadJob> jobs) => _jobs = jobs;
        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_jobs.FirstOrDefault(job => job.Id == id));
        public Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) => Task.FromResult(_jobs);
        public Task SaveAsync(DownloadJob job, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class MemoryFileStateRepository : IFileStateRepository
    {
        private readonly IReadOnlyList<FileState> _states;
        public MemoryFileStateRepository(IReadOnlyList<FileState> states) => _states = states;
        public Task<IReadOnlyList<FileState>> GetByJobAsync(Guid jobId, CancellationToken ct) => Task.FromResult<IReadOnlyList<FileState>>(_states.Where(state => state.JobId == jobId).ToList());
        public Task SaveAsync(FileState state, CancellationToken ct) => Task.CompletedTask;
    }
}
