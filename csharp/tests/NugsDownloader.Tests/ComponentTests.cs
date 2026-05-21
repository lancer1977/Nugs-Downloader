using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Web.Components.Pages;
using NugsDownloader.Web.Options;
using Xunit;

namespace NugsDownloader.Tests;

public class ComponentTests : BunitContext
{
    [Fact]
    public void HomePage_RendersDashboardAndSummaries()
    {
        var provider = new FakeProvider(capabilities: new ProviderCapabilities(true, true, true, true, true, true, new[] { "flac" }, new[] { "1080p" }));
        Services.AddSingleton<IProviderCatalog>(new InMemoryProviderCatalog(new[] { provider }));
        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(new[]
        {
            new DownloadJob(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), provider.Id, new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Downloads")
        }));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(new[]
        {
            new FileState(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Downloads/Demo/file.flac", FileKind.Audio, FileStatus.Complete, 0, 0, null, DateTimeOffset.UtcNow)
        }));

        var cut = Render<Home>();

        Assert.Contains("NugsDownloader", cut.Markup);
        Assert.Contains("/queue", cut.Markup);
        Assert.Contains("/login", cut.Markup);
        Assert.Contains("/file-state", cut.Markup);
        Assert.Contains("/provider-settings", cut.Markup);
        Assert.Contains(provider.DisplayName, cut.Markup);
        Assert.Contains("resume", cut.Markup);
        Assert.Contains("Recent Activity", cut.Markup);
        Assert.Contains("Downloads/Demo/file.flac", cut.Markup);
    }

    [Fact]
    public async Task QueuePage_SubmitFlow_CallsWorkflowAndShowsResult()
    {
        var provider = new FakeProvider(capabilities: new ProviderCapabilities(true, false, false, true, false, true, new[] { "flac" }, Array.Empty<string>()));
        var workflow = new RecordingWorkflow();
        var downloadDirectory = Path.Combine(Path.GetTempPath(), $"nugs-component-{Guid.NewGuid():N}", "downloads");
        var vault = new MemorySecretVault();
        var secretRef = await vault.StoreAsync(provider.Id, "Main", "stored-password", CancellationToken.None);
        var account = new ProviderAccount(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), provider.Id, "Main", "alice", secretRef, AuthenticationState.Valid, DateTimeOffset.UtcNow);

        Services.AddSingleton<IDownloadWorkflow>(workflow);
        Services.AddSingleton<IProviderCatalog>(new InMemoryProviderCatalog(new[] { provider }));
        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(Array.Empty<DownloadJob>()));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(Array.Empty<FileState>()));
        Services.AddSingleton<ICredentialStore>(new MemoryCredentialStore(new[] { account }));
        Services.AddSingleton<ISecretVault>(vault);
        Services.Configure<NugsDownloaderStorageOptions>(options => options.DownloadDirectory = downloadDirectory);

        var cut = Render<Queue>();

        cut.FindAll("input")[0].Change("https://example.com/release/1");

        await cut.InvokeAsync(() => cut.Find("button[type=submit]").Click());

        Assert.NotNull(workflow.Request);
        Assert.Equal(provider.Id, workflow.Request!.ProviderId);
        Assert.Equal("https://example.com/release/1", workflow.Request.SourceUrl.ToString());
        Assert.Equal("alice", workflow.Request.Credentials.Username);
        Assert.Equal("stored-password", workflow.Request.Credentials.Password);
        Assert.Equal("Main", workflow.Request.Credentials.Label);
        Assert.Equal(downloadDirectory, workflow.Request.Preferences.OutputRoot);
        Assert.Contains("Completed: ok", cut.Markup);
        Assert.Contains("Resume is available.", cut.Markup);
        Assert.Contains("Start Download", cut.Markup);
    }

    [Fact]
    public async Task LoginPage_ShowsHintsAndAuthResult()
    {
        var provider = new FakeProvider(
            capabilities: new ProviderCapabilities(true, false, false, false, true, true, Array.Empty<string>(), Array.Empty<string>()),
            authResult: new AuthResult(true, "nugs", "secret-ref", "Morning Show", DateTimeOffset.Parse("2030-01-01T00:00:00Z"), "Authenticated"));
        var credentials = new MemoryCredentialStore(Array.Empty<ProviderAccount>());
        var vault = new MemorySecretVault();

        Services.AddSingleton<IProviderCatalog>(new InMemoryProviderCatalog(new[] { provider }));
        Services.AddSingleton<ICredentialStore>(credentials);
        Services.AddSingleton<ISecretVault>(vault);

        var cut = Render<Login>();

        Assert.Contains("Password login is supported.", cut.Markup);
        Assert.Contains("Token login is supported.", cut.Markup);

        cut.FindAll("input")[0].Change("alice");
        cut.FindAll("input")[1].Change("secret");
        cut.FindAll("input")[2].Change("token-value");
        cut.FindAll("input")[3].Change("Morning Show");

        await cut.InvokeAsync(() => cut.Find("button[type=submit]").Click());

        Assert.NotNull(provider.LastCredentials);
        Assert.Equal("alice", provider.LastCredentials!.Username);
        Assert.Equal("secret", provider.LastCredentials.Password);
        Assert.Equal("token-value", provider.LastCredentials.Token);
        Assert.Contains("<strong>Status:</strong> Success", cut.Markup);
        Assert.Contains("Morning Show", cut.Markup);
        Assert.Contains("<strong>Secret:</strong> Saved", cut.Markup);
        Assert.DoesNotContain("secret-ref", cut.Markup);
        Assert.Single(credentials.Accounts);
        Assert.Equal("alice", credentials.Accounts[0].Username);
        Assert.Equal("Morning Show", credentials.Accounts[0].Label);
        Assert.Equal("secret", await vault.GetAsync(credentials.Accounts[0].SecretRef, CancellationToken.None));
    }

    [Fact]
    public void ProviderSettingsPage_RendersCapabilityMatrix()
    {
        var providers = new[]
        {
            new FakeProvider("nugs", "Nugs", new ProviderCapabilities(true, true, true, true, true, true, new[] { "flac", "aac" }, new[] { "1080p", "4k" })),
            new FakeProvider("livephish", "LivePhish", new ProviderCapabilities(true, false, false, false, false, false, Array.Empty<string>(), Array.Empty<string>()))
        };

        Services.AddSingleton<IProviderCatalog>(new InMemoryProviderCatalog(providers));
        Services.AddSingleton<ICredentialStore>(new MemoryCredentialStore(new[]
        {
            new ProviderAccount(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "nugs", "Main", "alice", "secret-ref", AuthenticationState.Valid, DateTimeOffset.UtcNow)
        }));

        var cut = Render<ProviderSettings>();

        Assert.Contains("Provider Settings", cut.Markup);
        Assert.Contains("Nugs", cut.Markup);
        Assert.Contains("LivePhish", cut.Markup);
        Assert.Contains("audio, video, chapters, resume, tokens, password login", cut.Markup);
        Assert.Contains("Formats: flac, aac", cut.Markup);
        Assert.Contains("Resolutions: 1080p, 4k", cut.Markup);
        Assert.Contains("Stored accounts: 1", cut.Markup);
        Assert.Contains("Main", cut.Markup);
        Assert.Contains("alice", cut.Markup);
    }

    [Fact]
    public void FileStatePage_RendersAllJobState()
    {
        var jobA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var jobB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(new[]
        {
            new DownloadJob(jobA, "nugs", new Uri("https://example.com/release/1"), "Demo A", DownloadJobStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, "Downloads"),
            new DownloadJob(jobB, "nugs", new Uri("https://example.com/release/2"), "Demo B", DownloadJobStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, "Downloads")
        }));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(new[]
        {
            new FileState(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), jobA, "Downloads/DemoA/file.flac", FileKind.Audio, FileStatus.Complete, 0, 0, null, DateTimeOffset.UtcNow),
            new FileState(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), jobB, "Downloads/DemoB/file.mp4", FileKind.Video, FileStatus.Partial, 0, 0, null, DateTimeOffset.UtcNow)
        }));

        var cut = Render<FileStatePage>();

        Assert.Contains("Summary", cut.Markup);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("<strong>Jobs:</strong> 2", cut.Markup);
            Assert.Contains("<strong>Files:</strong> 2", cut.Markup);
            Assert.Contains("Downloads/DemoA/file.flac", cut.Markup);
            Assert.Contains("Downloads/DemoB/file.mp4", cut.Markup);
        });
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
        Services.AddSingleton<IDownloadWorkflow>(new RecordingWorkflow());

        var cut = Render<JobDetails>(parameters => parameters.Add(p => p.JobId, jobId));

        Assert.Contains("Job Details", cut.Markup);
        Assert.Contains("Discovery Result", cut.Markup);
        Assert.Contains("Expected Files", cut.Markup);
        Assert.Contains("Demo", cut.Markup);
        Assert.Contains("Downloads/Demo/file.flac", cut.Markup);
    }

    [Fact]
    public async Task JobDetailsPage_ResumeAndRetryActionsCallWorkflow()
    {
        var jobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workflow = new RecordingWorkflow();
        Services.AddSingleton<IJobRepository>(new MemoryJobRepository(new[]
        {
            new DownloadJob(jobId, "nugs", new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Paused, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, "Downloads")
        }));
        Services.AddSingleton<IFileStateRepository>(new MemoryFileStateRepository(new[]
        {
            new FileState(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), jobId, "Downloads/Demo/file.flac", FileKind.Audio, FileStatus.Partial, 0, 0, null, DateTimeOffset.UtcNow)
        }));
        Services.AddSingleton<IDownloadWorkflow>(workflow);

        var cut = Render<JobDetails>(parameters => parameters.Add(p => p.JobId, jobId));

        await cut.InvokeAsync(() => cut.FindAll("button")[0].Click());
        Assert.Equal(jobId, workflow.ResumedJobId);
        Assert.Contains("ResumePrepared: ready", cut.Markup);

        await cut.InvokeAsync(() => cut.FindAll("button")[1].Click());
        Assert.Equal(jobId, workflow.RetriedJobId);
        Assert.Contains("RetryQueued: ready", cut.Markup);
    }

    private sealed class FakeProvider : IMediaProvider
    {
        public FakeProvider(string id = "nugs", string displayName = "Nugs", ProviderCapabilities? capabilities = null, AuthResult? authResult = null)
        {
            Id = id;
            DisplayName = displayName;
            Capabilities = capabilities ?? new ProviderCapabilities(true, true, true, true, true, true, Array.Empty<string>(), Array.Empty<string>());
            _authResult = authResult ?? new AuthResult(true, id, "secret", displayName, null, "Authenticated");
        }

        private readonly AuthResult _authResult;

        public string Id { get; }
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities { get; }
        public Credentials? LastCredentials { get; private set; }

        public bool CanHandle(Uri uri) => true;

        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct)
        {
            LastCredentials = credentials;
            return Task.FromResult(_authResult);
        }

        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) =>
            Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Demo", null, Array.Empty<MediaItem>(), false, true, new Dictionary<string, string>()));

        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) =>
            Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<FileState>(), null));

        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingWorkflow : IDownloadWorkflow
    {
        public StartDownloadRequest? Request { get; private set; }
        public Guid? ResumedJobId { get; private set; }
        public Guid? RetriedJobId { get; private set; }

        public Task<StartDownloadResult> StartAsync(StartDownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new StartDownloadResult(Guid.NewGuid(), request.ProviderId ?? "nugs", "Completed", "ok"));
        }

        public Task<StartDownloadResult> ResumeAsync(Guid jobId, CancellationToken ct)
        {
            ResumedJobId = jobId;
            return Task.FromResult(new StartDownloadResult(jobId, "nugs", "ResumePrepared", "ready"));
        }

        public Task<StartDownloadResult> RetryAsync(Guid jobId, CancellationToken ct)
        {
            RetriedJobId = jobId;
            return Task.FromResult(new StartDownloadResult(jobId, "nugs", "RetryQueued", "ready"));
        }
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

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        private readonly List<ProviderAccount> _accounts;

        public MemoryCredentialStore(IReadOnlyList<ProviderAccount> accounts) => _accounts = accounts.ToList();

        public IReadOnlyList<ProviderAccount> Accounts => _accounts;

        public Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct) =>
            Task.FromResult(_accounts.FirstOrDefault(account => account.ProviderId == providerId && account.Label == label));

        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>(_accounts);

        public Task SaveAsync(ProviderAccount account, CancellationToken ct)
        {
            _accounts.RemoveAll(existing => existing.Id == account.Id || existing.ProviderId == account.ProviderId && existing.Label == account.Label);
            _accounts.Add(account);
            return Task.CompletedTask;
        }
    }

    private sealed class MemorySecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _secrets = new();

        public Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct)
        {
            var secretRef = $"{providerId}:{label}";
            _secrets[secretRef] = secret;
            return Task.FromResult(secretRef);
        }

        public Task<string?> GetAsync(string secretRef, CancellationToken ct) =>
            Task.FromResult(_secrets.TryGetValue(secretRef, out var secret) ? secret : null);

        public Task DeleteAsync(string secretRef, CancellationToken ct)
        {
            _secrets.Remove(secretRef);
            return Task.CompletedTask;
        }
    }
}
