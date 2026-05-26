using System.Collections;
using NugsDownloader.App.Abstractions;
using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Infrastructure.Providers.LivePhish;
using Xunit;

namespace NugsDownloader.Tests;

public sealed class LivePhishLiveTests
{
    private const string EnableFlag = "LIVEPHISH_LIVE_TESTS";
    private const string DefaultSecretsPath = "/home/lancer1977/.config/secrets/livephish-downloader.env";
    private const string DefaultLiveUrl = "https://plus.livephish.com/index.html#/catalog/recording/12345";

    [Fact]
    [Trait("Category", "LivePhish")]
    public async Task LivePhishProvider_AuthenticatesWithConfiguredCredentials()
    {
        var secrets = LivePhishSecrets.Load();
        if (!secrets.Enabled)
        {
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(secrets.Username), "LIVEPHISH_USERNAME is required for live auth tests.");
        Assert.False(string.IsNullOrWhiteSpace(secrets.Password), "LIVEPHISH_PASSWORD is required for live auth tests.");

        var provider = new LivePhishMediaProvider();
        var result = await provider.AuthenticateAsync(
            new Credentials(secrets.Username!, secrets.Password!, secrets.Token, "Live integration"),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("livephish", result.ProviderId);
        Assert.False(string.IsNullOrWhiteSpace(result.SecretRef));
        Assert.StartsWith("Authenticated", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "LivePhish")]
    public async Task DownloadWorkflow_CompletesARepresentativeLivePhishDownload()
    {
        var secrets = LivePhishSecrets.Load();
        if (!secrets.Enabled)
        {
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(secrets.Username), "LIVEPHISH_USERNAME is required for live workflow tests.");
        Assert.False(string.IsNullOrWhiteSpace(secrets.Password), "LIVEPHISH_PASSWORD is required for live workflow tests.");

        var provider = new LivePhishMediaProvider();
        var catalog = new InMemoryProviderCatalog(new IMediaProvider[] { provider });
        var jobs = new MemoryJobRepository();
        var states = new MemoryFileStateRepository();
        var creds = new MemoryCredentialStore();
        var vault = new MemorySecretVault();
        var workflow = new DownloadWorkflow(catalog, jobs, states, creds, vault);

        var request = new StartDownloadRequest(
            null,
            provider.Id,
            new Uri(GetValue(EnvironmentVariables(), "LIVEPHISH_LIVE_DOWNLOAD_URL") ?? DefaultLiveUrl),
            new Credentials(secrets.Username!, secrets.Password!, secrets.Token, "Live integration"),
            new DownloadPreferences("flac", null, false, false, false, "Downloads", true, true));

        var progress = new RecordingProgress<DownloadProgress>();
        var result = await workflow.StartAsync(request, progress, CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.Contains("Processed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(progress.Reports);

        var job = (await jobs.ListAsync(CancellationToken.None)).Single();
        var fileStates = await states.GetByJobAsync(result.JobId, CancellationToken.None);
        var account = Assert.Single(await creds.ListAsync(CancellationToken.None));

        Assert.Equal(result.JobId, job.Id);
        Assert.Equal(DownloadJobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.NotEmpty(fileStates);
        Assert.All(fileStates, state =>
        {
            Assert.Equal(result.JobId, state.JobId);
            Assert.Equal(FileStatus.Complete, state.Status);
            Assert.Equal("Downloads", state.FilePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).First());
        });

        Assert.Equal(provider.Id, account.ProviderId);
        Assert.Equal("Live integration", account.Label);
        Assert.False(string.IsNullOrWhiteSpace(account.SecretRef));
        Assert.False(string.IsNullOrWhiteSpace(await vault.GetAsync(account.SecretRef, CancellationToken.None)));
    }

    private static IReadOnlyDictionary<string, string?> EnvironmentVariables()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                values[key] = value;
            }
        }

        var secretsPath = GetValue(values, "LIVEPHISH_SECRETS_FILE") ?? DefaultSecretsPath;
        if (File.Exists(secretsPath))
        {
            foreach (var line in File.ReadAllLines(secretsPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var name = trimmed[..separator].Trim();
                if (name.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                {
                    name = name["export ".Length..].Trim();
                }

                var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
                values[name] = value;
            }
        }

        return values;
    }

    private sealed record LivePhishSecrets(bool Enabled, string? Username, string? Password, string? Token)
    {
        public static LivePhishSecrets Load()
        {
            var values = EnvironmentVariables();
            var enabled = string.Equals(GetValue(values, EnableFlag), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetValue(values, EnableFlag), "true", StringComparison.OrdinalIgnoreCase);

            return new LivePhishSecrets(
                enabled,
                GetValue(values, "LIVEPHISH_USERNAME"),
                GetValue(values, "LIVEPHISH_PASSWORD"),
                GetValue(values, "LIVEPHISH_TOKEN"));
        }
    }

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = new();
        public void Report(T value) => Reports.Add(value);
    }

    private sealed class MemoryJobRepository : IJobRepository
    {
        private readonly List<DownloadJob> _jobs = new();
        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_jobs.FirstOrDefault(job => job.Id == id));
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
        private readonly List<ProviderAccount> _items = new();
        public Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct) => Task.FromResult<ProviderAccount?>(_items.FirstOrDefault(x => x.ProviderId == providerId && x.Label == label));
        public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>(_items.ToList());
        public Task SaveAsync(ProviderAccount account, CancellationToken ct) { _items.RemoveAll(x => x.Id == account.Id); _items.Add(account); return Task.CompletedTask; }
    }

    private sealed class MemorySecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _secrets = new();
        public Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct) { var key = $"{providerId}:{label}"; _secrets[key] = secret; return Task.FromResult(key); }
        public Task<string?> GetAsync(string secretRef, CancellationToken ct) => Task.FromResult(_secrets.TryGetValue(secretRef, out var secret) ? secret : null);
        public Task DeleteAsync(string secretRef, CancellationToken ct) { _secrets.Remove(secretRef); return Task.CompletedTask; }
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
