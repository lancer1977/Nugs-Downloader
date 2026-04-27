using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class ProviderCatalogTests
{
    [Fact]
    public void InMemoryProviderCatalog_FindsByProviderIdAndUrl()
    {
        var provider = new FakeProvider();
        var catalog = new InMemoryProviderCatalog(new[] { provider });

        Assert.Same(provider, catalog.FindById("fake"));
        Assert.Same(provider, catalog.FindByUrl(new Uri("https://example.com/test")));
    }

    private sealed class FakeProvider : IMediaProvider
    {
        public string Id => "fake";
        public string DisplayName => "Fake";
        public ProviderCapabilities Capabilities { get; } = new(true, false, false, false, false, false, Array.Empty<string>(), Array.Empty<string>());
        public bool CanHandle(Uri uri) => uri.Host == "example.com";
        public Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct) => Task.FromResult(new AuthResult(true, Id, "secret", DisplayName, null, null));
        public Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct) => Task.FromResult(new MediaDiscoveryResult(Id, uri, uri, "Title", null, Array.Empty<MediaItem>(), false, true, new Dictionary<string, string>()));
        public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct) => Task.FromResult(new DownloadPlan(Id, Guid.NewGuid(), discovery.Items, preferences.OutputRoot, preferences, Array.Empty<NugsDownloader.Domain.Entities.FileState>(), null));
        public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct) => Task.CompletedTask;
    }
}
