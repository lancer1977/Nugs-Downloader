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
        var workflow = new DownloadWorkflow(
            catalog,
            new MemoryJobRepository(),
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

        Assert.Equal("AuthFailed", result.Status);
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
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<FileState>(), null));
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
