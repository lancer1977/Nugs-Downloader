using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class WorkflowTests
{
    [Fact]
    public async Task DownloadWorkflow_SavesJobFileStateAndCredentials()
    {
        var provider = new FakeProvider();
        var catalog = new InMemoryProviderCatalog(new IMediaProvider[] { provider });
        var jobs = new MemoryJobRepository();
        var states = new MemoryFileStateRepository();
        var creds = new MemoryCredentialStore();
        var vault = new MemorySecretVault();
        var workflow = new DownloadWorkflow(catalog, jobs, states, creds, vault);

        var request = new StartDownloadRequest(
            null,
            "fake",
            new Uri("https://example.com/album/123"),
            new Credentials("user", "pass", null, "label"),
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true));

        var result = await workflow.StartAsync(request, new Progress<DownloadProgress>(), CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.Single(await jobs.ListAsync(CancellationToken.None));
        Assert.Single(await states.GetByJobAsync(result.JobId, CancellationToken.None));
        Assert.Single(creds.Items);
        Assert.Equal("pass", await vault.GetAsync(creds.Items[0].SecretRef, CancellationToken.None));
        Assert.Equal("label", creds.Items[0].Label);
        Assert.Equal("fake", provider.CallsProviderId);
    }

    [Fact]
    public async Task DownloadWorkflow_UsesExplicitProviderId()
    {
        var provider = new FakeProvider();
        var catalog = new InMemoryProviderCatalog(new IMediaProvider[] { provider });
        var workflow = new DownloadWorkflow(
            catalog,
            new MemoryJobRepository(),
            new MemoryFileStateRepository(),
            new MemoryCredentialStore(),
            new MemorySecretVault());

        var request = new StartDownloadRequest(
            null,
            "fake",
            new Uri("https://example.com/not-matched"),
            new Credentials("user", "pass", null, "label"),
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true));

        var result = await workflow.StartAsync(request, new Progress<DownloadProgress>(), CancellationToken.None);

        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task DownloadWorkflow_ReturnsAuthFailedWhenProviderRejectsCredentials()
    {
        var provider = new RejectingProvider();
        var catalog = new InMemoryProviderCatalog(new IMediaProvider[] { provider });
        var jobs = new MemoryJobRepository();
        var workflow = new DownloadWorkflow(
            catalog,
            jobs,
            new MemoryFileStateRepository(),
            new MemoryCredentialStore(),
            new MemorySecretVault());

        var request = new StartDownloadRequest(
            null,
            "reject",
            new Uri("https://example.com/release/123"),
            new Credentials("user", "pass", null, "label"),
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true));

        var result = await workflow.StartAsync(request, new Progress<DownloadProgress>(), CancellationToken.None);
        var job = (await jobs.ListAsync(CancellationToken.None)).Single();

        Assert.Equal("AuthFailed", result.Status);
        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("nope", job.ErrorMessage);
    }

    [Fact]
    public async Task DownloadWorkflow_PersistsExpectedFilesFromPlan()
    {
        var provider = new FakeProvider();
        var states = new MemoryFileStateRepository();
        var workflow = new DownloadWorkflow(
            new InMemoryProviderCatalog(new IMediaProvider[] { provider }),
            new MemoryJobRepository(),
            states,
            new MemoryCredentialStore(),
            new MemorySecretVault());

        var result = await workflow.StartAsync(
            new StartDownloadRequest(
                null,
                "fake",
                new Uri("https://example.com/album/123"),
                new Credentials("user", "pass", null, "label"),
                new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true)),
            new Progress<DownloadProgress>(),
            CancellationToken.None);

        var fileStates = await states.GetByJobAsync(result.JobId, CancellationToken.None);

        Assert.Single(fileStates);
        Assert.Equal(result.JobId, fileStates[0].JobId);
        Assert.Equal(FileStatus.Complete, fileStates[0].Status);
        Assert.NotNull(fileStates[0].LastVerifiedAt);
    }

    [Fact]
    public async Task DownloadWorkflow_ResumeMarksMissingPartialFilesAndReadiesJob()
    {
        var jobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var jobs = new MemoryJobRepository();
        var states = new MemoryFileStateRepository();
        await jobs.SaveAsync(new DownloadJob(jobId, "fake", new Uri("https://example.com/album/123"), "Demo", DownloadJobStatus.Paused, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, "Downloads"), CancellationToken.None);
        await states.SaveAsync(new FileState(Guid.NewGuid(), jobId, "/tmp/nugs-missing-file.flac", FileKind.Audio, FileStatus.Partial, 10, 5, null, DateTimeOffset.UtcNow), CancellationToken.None);

        var workflow = new DownloadWorkflow(
            new InMemoryProviderCatalog(new IMediaProvider[] { new FakeProvider() }),
            jobs,
            states,
            new MemoryCredentialStore(),
            new MemorySecretVault());

        var result = await workflow.ResumeAsync(jobId, CancellationToken.None);
        var job = await jobs.GetAsync(jobId, CancellationToken.None);
        var fileStates = await states.GetByJobAsync(jobId, CancellationToken.None);

        Assert.Equal("ResumePrepared", result.Status);
        Assert.Equal(DownloadJobStatus.Ready, job!.Status);
        Assert.Equal(FileStatus.Missing, fileStates[0].Status);
    }

    [Fact]
    public async Task DownloadWorkflow_RetryClearsFailedJobState()
    {
        var jobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var jobs = new MemoryJobRepository();
        await jobs.SaveAsync(new DownloadJob(jobId, "fake", new Uri("https://example.com/album/123"), "Demo", DownloadJobStatus.Failed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "boom", "Downloads"), CancellationToken.None);
        var workflow = new DownloadWorkflow(
            new InMemoryProviderCatalog(new IMediaProvider[] { new FakeProvider() }),
            jobs,
            new MemoryFileStateRepository(),
            new MemoryCredentialStore(),
            new MemorySecretVault());

        var result = await workflow.RetryAsync(jobId, CancellationToken.None);
        var job = await jobs.GetAsync(jobId, CancellationToken.None);

        Assert.Equal("RetryQueued", result.Status);
        Assert.Equal(DownloadJobStatus.Ready, job!.Status);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public async Task DownloadWorkflow_MarksJobFailedWhenProviderExecutionFails()
    {
        var jobs = new MemoryJobRepository();
        var workflow = new DownloadWorkflow(
            new InMemoryProviderCatalog(new IMediaProvider[] { new ThrowingProvider() }),
            jobs,
            new MemoryFileStateRepository(),
            new MemoryCredentialStore(),
            new MemorySecretVault());

        var result = await workflow.StartAsync(
            new StartDownloadRequest(
                null,
                "throwing",
                new Uri("https://example.com/album/123"),
                new Credentials("user", "pass", null, "label"),
                new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true)),
            new Progress<DownloadProgress>(),
            CancellationToken.None);

        var job = (await jobs.ListAsync(CancellationToken.None)).Single();

        Assert.Equal("Failed", result.Status);
        Assert.Equal("provider exploded", result.Message);
        Assert.Equal(DownloadJobStatus.Failed, job.Status);
        Assert.Equal("Title", job.DisplayName);
        Assert.Equal("provider exploded", job.ErrorMessage);
    }

    [Fact]
    public async Task DownloadWorkflow_MarksJobCancelledWhenExecutionTokenIsCancelled()
    {
        var jobs = new MemoryJobRepository();
        var workflow = new DownloadWorkflow(
            new InMemoryProviderCatalog(new IMediaProvider[] { new CancellingProvider() }),
            jobs,
            new MemoryFileStateRepository(),
            new MemoryCredentialStore(),
            new MemorySecretVault());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await workflow.StartAsync(
            new StartDownloadRequest(
                null,
                "cancelling",
                new Uri("https://example.com/album/123"),
                new Credentials("user", "pass", null, "label"),
                new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true)),
            new Progress<DownloadProgress>(),
            cts.Token);

        var job = (await jobs.ListAsync(CancellationToken.None)).Single();

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal(DownloadJobStatus.Cancelled, job.Status);
        Assert.Equal("Download was cancelled.", job.ErrorMessage);
    }

    private sealed class FakeProvider : IMediaProvider
    {
        public string Id => "fake";
        public string DisplayName => "Fake";
        public string? CallsProviderId { get; private set; }
        public ProviderCapabilities Capabilities { get; } = new(true, false, false, false, false, false, Array.Empty<string>(), Array.Empty<string>());
        public bool CanHandle(Uri uri) => uri.Host == "example.com";
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(true, Id, "secret-ref", DisplayName, null, null));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Title", null, new[] { new MediaItem("1", "Item", "audio", 0, new Dictionary<string, string>()) }, false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, new[]
        {
            new FileState(Guid.NewGuid(), Guid.Empty, Path.Combine(preferences.OutputRoot, "Title", "01 - Item.flac"), FileKind.Audio, FileStatus.Partial, 0, 0, null, null)
        }, null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            CallsProviderId = plan.ProviderId;
            progress.Report(new DownloadProgress(plan.JobId, plan.ProviderId, 1, 1, 100, 1, 1, "done"));
            return Task.CompletedTask;
        }
    }

    private sealed class RejectingProvider : IMediaProvider
    {
        public string Id => "reject";
        public string DisplayName => "Reject";
        public ProviderCapabilities Capabilities { get; } = new(true, false, false, false, false, false, Array.Empty<string>(), Array.Empty<string>());
        public bool CanHandle(Uri uri) => true;
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(false, Id, null, DisplayName, null, "nope"));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Title", null, Array.Empty<MediaItem>(), false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<FileState>(), null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ThrowingProvider : IMediaProvider
    {
        public string Id => "throwing";
        public string DisplayName => "Throwing";
        public ProviderCapabilities Capabilities { get; } = new(true, false, false, false, false, false, Array.Empty<string>(), Array.Empty<string>());
        public bool CanHandle(Uri uri) => true;
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(true, Id, "secret-ref", DisplayName, null, null));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Title", null, new[] { new MediaItem("1", "Item", "audio", 0, new Dictionary<string, string>()) }, false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<FileState>(), null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct) => throw new InvalidOperationException("provider exploded");
    }

    private sealed class CancellingProvider : IMediaProvider
    {
        public string Id => "cancelling";
        public string DisplayName => "Cancelling";
        public ProviderCapabilities Capabilities { get; } = new(true, false, false, false, false, false, Array.Empty<string>(), Array.Empty<string>());
        public bool CanHandle(Uri uri) => true;
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(true, Id, "secret-ref", DisplayName, null, null));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Title", null, new[] { new MediaItem("1", "Item", "audio", 0, new Dictionary<string, string>()) }, false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<FileState>(), null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryJobRepository : IJobRepository
    {
        private readonly List<DownloadJob> _jobs = new();
        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_jobs.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadJob>>(_jobs.ToList());
        public Task SaveAsync(DownloadJob job, CancellationToken ct) { _jobs.RemoveAll(x => x.Id == job.Id); _jobs.Add(job); return Task.CompletedTask; }
    }

    private sealed class MemoryFileStateRepository : IFileStateRepository
    {
        private readonly List<FileState> _states = new();
        public Task<IReadOnlyList<FileState>> GetByJobAsync(Guid jobId, CancellationToken ct) => Task.FromResult<IReadOnlyList<FileState>>(_states.Where(x => x.JobId == jobId).ToList());
        public Task SaveAsync(FileState state, CancellationToken ct) { _states.RemoveAll(x => x.Id == state.Id); _states.Add(state); return Task.CompletedTask; }
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        public List<ProviderAccount> Items { get; } = new();
        public Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct) => Task.FromResult<ProviderAccount?>(Items.FirstOrDefault(x => x.ProviderId == providerId && x.Label == label));
        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>(Items.ToList());
        public Task SaveAsync(ProviderAccount account, CancellationToken ct) { Items.RemoveAll(x => x.Id == account.Id); Items.Add(account); return Task.CompletedTask; }
    }

    private sealed class MemorySecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _secrets = new();
        public Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct) { var key = $"{providerId}:{label}"; _secrets[key] = secret; return Task.FromResult(key); }
        public Task<string?> GetAsync(string secretRef, CancellationToken ct) => Task.FromResult(_secrets.TryGetValue(secretRef, out var secret) ? secret : null);
        public Task DeleteAsync(string secretRef, CancellationToken ct) { _secrets.Remove(secretRef); return Task.CompletedTask; }
    }
}
