using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class WebIntegrationTests
{
    [Theory]
    [InlineData("/", "NugsDownloader")]
    [InlineData("/queue", "Queue")]
    [InlineData("/login", "Login")]
    [InlineData("/file-state", "File State")]
    [InlineData("/provider-settings", "Provider Settings")]
    public async Task WebHost_RendersPrimaryPages(string path, string expectedText)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains(expectedText, body);
        if (path != "/file-state")
        {
            Assert.Contains("Integration Nugs", body);
        }
    }

    [Fact]
    public async Task WebHost_RendersInteractiveServerMarkers()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/login");

        Assert.Contains("\"type\":\"server\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_framework/blazor.web.js", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebHost_RendersJobDetailsFromRegisteredRepositories()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/jobs/{TestWebApplicationFactory.JobId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Job Details", body);
        Assert.Contains("Integration Job", body);
        Assert.Contains("Discovery Result", body);
        Assert.Contains("Downloads/Integration/file.flac", body);
    }

    [Fact]
    public async Task WebHost_WiresWorkflowAndProviderServices()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var workflow = scope.ServiceProvider.GetRequiredService<IDownloadWorkflow>();
        var catalog = scope.ServiceProvider.GetRequiredService<IProviderCatalog>();

        var result = await workflow.StartAsync(
            new StartDownloadRequest(
                null,
                "nugs",
                new Uri("https://play.nugs.net/release/123"),
                new Credentials("alice", "secret", null, "Main"),
                new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true)),
            new Progress<DownloadProgress>(),
            CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.NotNull(catalog.FindById("nugs"));
    }

    [Theory]
    [InlineData("/app.css")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.css")]
    public async Task WebHost_ServesThemeAssets(string path)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength is null or > 0);
    }

    [Fact]
    public async Task WebHost_ServesHealthEndpoint()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body);
    }

    [Fact]
    public async Task WebHost_ServesReadyEndpointForConfiguredStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nugs-web-{Guid.NewGuid():N}");
        var stateDirectory = Path.Combine(root, "state");
        var downloadDirectory = Path.Combine(root, "downloads");
        await using var factory = new TestWebApplicationFactory(stateDirectory, downloadDirectory);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body);
        Assert.True(Directory.Exists(stateDirectory));
        Assert.True(Directory.Exists(downloadDirectory));
    }

    [Fact]
    public async Task Queue_UsesConfiguredDownloadDirectoryAsDefaultOutputRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nugs-web-{Guid.NewGuid():N}");
        var downloadDirectory = Path.Combine(root, "downloads");
        await using var factory = new TestWebApplicationFactory(
            Path.Combine(root, "state"),
            downloadDirectory);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/queue");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains(downloadDirectory, body);
    }

    private sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        public static readonly Guid JobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private readonly string? _stateDirectory;
        private readonly string? _downloadDirectory;

        public TestWebApplicationFactory(string? stateDirectory = null, string? downloadDirectory = null)
        {
            _stateDirectory = stateDirectory;
            _downloadDirectory = downloadDirectory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            if (!string.IsNullOrWhiteSpace(_stateDirectory))
            {
                builder.UseSetting("NugsDownloader:StateDirectory", _stateDirectory);
            }

            if (!string.IsNullOrWhiteSpace(_downloadDirectory))
            {
                builder.UseSetting("NugsDownloader:DownloadDirectory", _downloadDirectory);
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMediaProvider>();
                services.RemoveAll<IProviderCatalog>();
                services.RemoveAll<IJobRepository>();
                services.RemoveAll<IFileStateRepository>();
                services.RemoveAll<ICredentialStore>();
                services.RemoveAll<ISecretVault>();

                var jobRepository = new MemoryJobRepository(new[]
                {
                    new DownloadJob(
                        JobId,
                        "nugs",
                        new Uri("https://play.nugs.net/release/123"),
                        "Integration Job",
                        DownloadJobStatus.Completed,
                        DateTimeOffset.UtcNow.AddMinutes(-5),
                        DateTimeOffset.UtcNow.AddMinutes(-4),
                        DateTimeOffset.UtcNow.AddMinutes(-1),
                        null,
                        "Downloads")
                });
                var fileStateRepository = new MemoryFileStateRepository(new[]
                {
                    new FileState(
                        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                        JobId,
                        "Downloads/Integration/file.flac",
                        FileKind.Audio,
                        FileStatus.Complete,
                        0,
                        0,
                        null,
                        DateTimeOffset.UtcNow)
                });
                var credentialStore = new MemoryCredentialStore(new[]
                {
                    new ProviderAccount(
                        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                        "nugs",
                        "Main",
                        "alice",
                        "secret-ref",
                        AuthenticationState.Valid,
                        DateTimeOffset.UtcNow)
                });

                services.AddSingleton<IMediaProvider>(new IntegrationProvider());
                services.AddSingleton<IProviderCatalog, InMemoryProviderCatalog>(sp =>
                    new InMemoryProviderCatalog(sp.GetServices<IMediaProvider>()));
                services.AddSingleton<IJobRepository>(jobRepository);
                services.AddSingleton<IFileStateRepository>(fileStateRepository);
                services.AddSingleton<ICredentialStore>(credentialStore);
                services.AddSingleton<ISecretVault, MemorySecretVault>();
            });
        }
    }

    private sealed class IntegrationProvider : IMediaProvider
    {
        public string Id => "nugs";
        public string DisplayName => "Integration Nugs";
        public ProviderCapabilities Capabilities { get; } = new(true, true, true, true, true, true, new[] { "flac", "aac" }, new[] { "1080p", "4k" });
        public bool CanHandle(Uri uri) => uri.Host.Contains("nugs.net", StringComparison.OrdinalIgnoreCase);
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(true, Id, "secret-ref", "Integration Nugs", null, "Authenticated"));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Integration Job", "Artist", new[] { new MediaItem("1", "file", "audio", 0, new Dictionary<string, string>()) }, false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, new[]
        {
            new FileState(Guid.NewGuid(), Guid.Empty, "Downloads/Integration/file.flac", FileKind.Audio, FileStatus.Partial, 0, 0, null, null)
        }, null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            progress.Report(new DownloadProgress(plan.JobId, plan.ProviderId, 1, 1, 100, 1, 1, "done"));
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryJobRepository : IJobRepository
    {
        private readonly List<DownloadJob> _jobs;
        public MemoryJobRepository(IEnumerable<DownloadJob> jobs) => _jobs = jobs.ToList();
        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_jobs.FirstOrDefault(job => job.Id == id));
        public Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadJob>>(_jobs.ToList());
        public Task SaveAsync(DownloadJob job, CancellationToken ct)
        {
            _jobs.RemoveAll(existing => existing.Id == job.Id);
            _jobs.Add(job);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryFileStateRepository : IFileStateRepository
    {
        private readonly List<FileState> _states;
        public MemoryFileStateRepository(IEnumerable<FileState> states) => _states = states.ToList();
        public Task<IReadOnlyList<FileState>> GetByJobAsync(Guid jobId, CancellationToken ct) => Task.FromResult<IReadOnlyList<FileState>>(_states.Where(state => state.JobId == jobId).ToList());
        public Task SaveAsync(FileState state, CancellationToken ct)
        {
            _states.RemoveAll(existing => existing.Id == state.Id);
            _states.Add(state);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryCredentialStore : ICredentialStore
    {
        private readonly List<ProviderAccount> _accounts;
        public MemoryCredentialStore(IEnumerable<ProviderAccount> accounts) => _accounts = accounts.ToList();
        public Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct) => Task.FromResult(_accounts.FirstOrDefault(account => account.ProviderId == providerId && account.Label == label));
        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>(_accounts.ToList());
        public Task SaveAsync(ProviderAccount account, CancellationToken ct)
        {
            _accounts.RemoveAll(existing => existing.Id == account.Id);
            _accounts.Add(account);
            return Task.CompletedTask;
        }
    }

    private sealed class MemorySecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _secrets = new();
        public Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct)
        {
            var key = $"{providerId}:{label}";
            _secrets[key] = secret;
            return Task.FromResult(key);
        }

        public Task<string?> GetAsync(string secretRef, CancellationToken ct) => Task.FromResult(_secrets.GetValueOrDefault(secretRef));
        public Task DeleteAsync(string secretRef, CancellationToken ct)
        {
            _secrets.Remove(secretRef);
            return Task.CompletedTask;
        }
    }
}
