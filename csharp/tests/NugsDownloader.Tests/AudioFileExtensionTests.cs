using System.Net;
using System.Net.Http.Json;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Infrastructure.Providers.Nugs;
using Xunit;

namespace NugsDownloader.Tests;

public sealed class AudioFileExtensionTests
{
    [Fact]
    public async Task Nugs_DiscoveryThroughDownload_UsesActualUrlExtensionWithoutQuery()
    {
        var root = Path.Combine(Path.GetTempPath(), "audio-extension-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[] { 1, 2, 3, 4 };
        using var http = new HttpClient(new FixtureHandler(request =>
            request.RequestUri!.Host == "media.test"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { response = new {
                        artistName = "Artist", containerInfo = "Show", tracks = new[] {
                            new { trackID = 1, songTitle = "Doner. Wrap", clipURL = "https://media.test/song.m4a?signature=.flac" }
                        }
                    } })
                }));
        try
        {
            var provider = new NugsMediaProvider(new NugsApiClient(http));
            var discovery = await provider.DiscoverAsync(new Uri("https://play.nugs.net/release/50916"), CancellationToken.None);
            var plan = await provider.BuildDownloadPlanAsync(discovery,
                new DownloadPreferences(null, null, false, false, false, root, false, false), CancellationToken.None);
            await provider.ExecuteDownloadAsync(plan, new InlineProgress(), CancellationToken.None);

            var state = Assert.Single(plan.ExpectedFiles);
            Assert.Equal(Path.Combine(root, "Artist", "Show", "01. Doner. Wrap.m4a"), state.FilePath);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(state.FilePath));
            Assert.False(File.Exists(state.FilePath + ".partial"));
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nugs_EntitledExtensionlessDownload_PersistsContainerPathInsteadOfRequestedCodec(bool failSecondTrack)
    {
        var root = Path.Combine(Path.GetTempPath(), "audio-extension-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[] { 1, 2, 3, 4 };
        var mediaRequests = new List<string>();
        using var http = new HttpClient(new FixtureHandler(request =>
        {
            var uri = request.RequestUri!;
            if (uri.Host == "media.test")
            {
                mediaRequests.Add(uri.AbsolutePath);
                if (uri.AbsolutePath == "/entitled2")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new FailingContent() };
                }
                Assert.Equal("/entitled", uri.AbsolutePath);
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mp4");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }
            object? json = uri.AbsoluteUri switch
            {
                var url when url.Contains("connect/token") => new { access_token = "fixture-token" },
                var url when url.Contains("connect/userinfo") => new { sub = "fixture-user" },
                var url when url.Contains("me/subscriptions") => new {
                    plan = new { planID = "fixture-plan" }, legacySubscriptionID = "fixture-subscription",
                    startedAt = "01/02/2026 03:04:05", endsAt = "12/31/2026 23:59:59", isContentAccessible = true
                },
                var url when url.Contains("subPlayer.aspx") => new { streamLink = url.Contains("trackId=2&")
                    ? "https://media.test/entitled2" : "https://media.test/entitled?signature=.aac" },
                var url when url.Contains("catalog.container") => new { response = new {
                    artistName = "Artist", containerInfo = "Show", tracks = new[] {
                        new { trackID = 1, songTitle = "Doner. Wrap", clipURL = "https://media.test/clip.flac" },
                        new { trackID = 2, songTitle = "Failed Track", clipURL = "https://media.test/clip.flac" }
                    }.Take(failSecondTrack ? 2 : 1).ToArray()
                } },
                _ => null
            };
            return json is null ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(json) };
        }));
        try
        {
            var provider = new NugsMediaProvider(new NugsApiClient(http));
            var stores = new MemoryStores();
            var workflow = new NugsDownloader.App.UseCases.DownloadWorkflow(
                new NugsDownloader.App.Abstractions.InMemoryProviderCatalog(new[] { provider }), stores, stores, stores, stores);
            var result = await workflow.StartAsync(new NugsDownloader.App.UseCases.StartDownloadRequest(
                null, "nugs", new Uri("https://play.nugs.net/release/50916"),
                new Credentials("fixture-user", "fixture-password", null, "fixture"),
                new DownloadPreferences("flac", null, false, false, false, root, true, true)), new InlineProgress(), CancellationToken.None);

            Assert.Equal(failSecondTrack ? "Failed" : "Completed", result.Status);
            var audio = Assert.Single(stores.Files, state => state.FilePath.Contains("Doner. Wrap", StringComparison.Ordinal));
            Assert.Equal(Path.Combine(root, "Artist", "Show", "01. Doner. Wrap.m4a"), audio.FilePath);
            Assert.Equal(failSecondTrack ? FileStatus.Partial : FileStatus.Complete, audio.Status);
            Assert.Equal(bytes.Length, audio.ActualSize);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(audio.FilePath));
            Assert.Equal(failSecondTrack ? 2 : 1, mediaRequests.Count);
            if (failSecondTrack)
            {
                var failed = Assert.Single(stores.Files, state => state.FilePath.Contains("Failed Track", StringComparison.Ordinal));
                Assert.Equal(FileStatus.Partial, failed.Status);
                Assert.False(File.Exists(failed.FilePath));
                Assert.Equal(0, failed.ActualSize);
            }
            else
            {
                Assert.All(stores.Files, state => Assert.True(File.Exists(state.FilePath)));
            }
            Assert.Empty(Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories));
            Assert.Equal(failSecondTrack ? 1 : 3, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("nugs", "https://media.test/track.aac?token=.flac", "audio/aac", "00000018667479704D3441200000000069736F6D4D344120", "Song. Part.m4a", ".m4a")]
    [InlineData("livephish", "https://media.test/track.aac?token=.flac", "audio/aac", "00000018667479704D3441200000000069736F6D4D344120", "Song. Part.m4a", ".m4a")]
    [InlineData("nugs", "https://media.test/entitled", "application/octet-stream", "000000186674797069736F6D0000000069736F6D6D703432", "Song. Part", ".mp4")]
    [InlineData("livephish", "https://media.test/entitled", "application/octet-stream", "664C614300000000", "Song. Part", ".flac")]
    [InlineData("nugs", "https://media.test/entitled", "audio/mp4", "FFF1508000", "Song. Part", ".aac")]
    [InlineData("nugs", "https://media.test/entitled", "application/octet-stream", "524946462400000057415645666D7420", "Song. Part", ".wav")]
    [InlineData("nugs", "https://media.test/entitled", "application/octet-stream", "000000186674797068656963000000006865696300000000", "Song. Part", ".bin")]
    [InlineData("nugs", "https://media.test/entitled", "application/octet-stream", "01020304", "Song. Part.flac", ".bin")]
    [InlineData("livephish", "https://media.test/entitled", "application/octet-stream", "01020304", "Song. Part.flac", ".bin")]
    [InlineData("nugs", "https://media.test/entitled", "application/octet-stream", "01020304", "Song. Part", ".bin")]
    [InlineData("livephish", "https://media.test/entitled", "application/octet-stream", "01020304", "Song. Part", ".bin")]
    [InlineData("livephish", "https://media.test/entitled", "audio/flac", "01020304", "Song. Part", ".flac")]
    [InlineData("nugs", "https://media.test/entitled", "audio/mpeg", "01020304", "Song. Part", ".mp3")]
    [InlineData("nugs", "https://media.test/track.m4a?token=.flac", "application/octet-stream", "01020304", "Song. Part.m4a", ".m4a")]
    public async Task AudioDownload_UsesSignatureBeforeConflictingMetadata(string providerId, string url, string mime, string hex, string title, string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "audio-extension-" + Guid.NewGuid().ToString("N"));
        var bytes = Convert.FromHexString(hex); // Minimal container headers, not playable media fixtures.
        using var http = new HttpClient(new FixtureHandler(_ =>
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        NugsDownloader.Domain.Providers.IMediaProvider provider = providerId == "nugs"
            ? new NugsMediaProvider(new NugsApiClient(http))
            : new NugsDownloader.Infrastructure.Providers.LivePhish.LivePhishMediaProvider(
                new NugsDownloader.Infrastructure.Providers.LivePhish.LivePhishApiClient(http));
        try
        {
            var source = new Uri("https://example.test/release/1");
            var discovery = new MediaDiscoveryResult(providerId, source, source, "Show", "Artist", new[] {
                new MediaItem("1", title, "audio", 0, new Dictionary<string, string> { ["downloadUrl"] = url })
            }, false, true, new Dictionary<string, string>());
            var plan = await provider.BuildDownloadPlanAsync(discovery,
                new DownloadPreferences("aac", null, false, false, false, root, false, false), CancellationToken.None);
            await provider.ExecuteDownloadAsync(plan, new InlineProgress(), CancellationToken.None);
            var state = Assert.Single(plan.ExpectedFiles);
            Assert.Equal(Path.Combine(root, "Artist", "Show", "01. Song. Part" + extension), state.FilePath);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(state.FilePath));
            Assert.Single(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            Assert.False(File.Exists(state.FilePath + ".partial"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("nugs", null, "Song. Part", "01. Song. Part.bin")]
    [InlineData("nugs", "alac", "Song. Part", "01. Song. Part.bin")]
    [InlineData("nugs", "auto", "Song. Part.m4a", "01. Song. Part.m4a")]
    [InlineData("livephish", "aac", "Song. Part", "01. Song. Part.bin")]
    public async Task AudioPlanning_DoesNotGuessContainerFromRequestedCodec(string providerId, string? preference, string title, string fileName)
    {
        using var http = new HttpClient(new FixtureHandler(_ => throw new InvalidOperationException("Planning must not fetch media.")));
        NugsDownloader.Domain.Providers.IMediaProvider provider = providerId == "nugs"
            ? new NugsMediaProvider(new NugsApiClient(http))
            : new NugsDownloader.Infrastructure.Providers.LivePhish.LivePhishMediaProvider(
                new NugsDownloader.Infrastructure.Providers.LivePhish.LivePhishApiClient(http));
        var source = new Uri("https://example.test/release/1");
        var discovery = new MediaDiscoveryResult(providerId, source, source, "Show", null,
            new[] { new MediaItem("1", title, "audio", 0, new Dictionary<string, string>()) }, false, true, new Dictionary<string, string>());
        var plan = await provider.BuildDownloadPlanAsync(discovery,
            new DownloadPreferences(preference, null, false, false, false, "Downloads", false, false), CancellationToken.None);
        Assert.Equal(Path.Combine("Downloads", "Show", fileName), Assert.Single(plan.ExpectedFiles).FilePath);
    }

    [Theory]
    [InlineData("nugs", false)]
    [InlineData("nugs", true)]
    [InlineData("livephish", false)]
    [InlineData("livephish", true)]
    public async Task AudioDownload_FailureRemovesPartialAndPreservesExistingTarget(string providerId, bool empty)
    {
        var root = Path.Combine(Path.GetTempPath(), "audio-extension-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "01. Song.m4a");
        Directory.CreateDirectory(root);
        var original = new byte[] { 7, 8, 9 };
        await File.WriteAllBytesAsync(target, original);
        using var http = new HttpClient(new FixtureHandler(_ =>
        {
            Assert.True(File.Exists(target + ".partial"));
            Assert.Equal(original, File.ReadAllBytes(target));
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = empty ? new ByteArrayContent(Array.Empty<byte>()) : new FailingContent()
            };
        }));
        NugsDownloader.Domain.Providers.IMediaProvider provider = providerId == "nugs"
            ? new NugsMediaProvider(new NugsApiClient(http))
            : new NugsDownloader.Infrastructure.Providers.LivePhish.LivePhishMediaProvider(
                new NugsDownloader.Infrastructure.Providers.LivePhish.LivePhishApiClient(http));
        try
        {
            var state = new FileState(Guid.NewGuid(), Guid.Empty, target, FileKind.Audio, FileStatus.Partial, 0, 0, null, null);
            var plan = new DownloadPlan(providerId, Guid.NewGuid(), new[] {
                new MediaItem("1", "Song", "audio", 0, new Dictionary<string, string> { ["downloadUrl"] = "https://media.test/song.m4a" })
            }, root, new DownloadPreferences(null, null, false, false, false, root, false, false), new[] { state }, null);
            var error = await Assert.ThrowsAnyAsync<Exception>(() => provider.ExecuteDownloadAsync(plan, new InlineProgress(), CancellationToken.None));
            Assert.True(error is IOException or HttpRequestException or InvalidOperationException, error.ToString());
            Assert.Equal(target, Assert.Single(plan.ExpectedFiles).FilePath);
            Assert.Equal(original, await File.ReadAllBytesAsync(target));
            Assert.False(File.Exists(target + ".partial"));
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class MemoryStores : NugsDownloader.App.Abstractions.IJobRepository,
        NugsDownloader.App.Abstractions.IFileStateRepository, NugsDownloader.App.Abstractions.ICredentialStore,
        NugsDownloader.App.Abstractions.ISecretVault
    {
        public List<FileState> Files { get; } = new();
        private readonly List<DownloadJob> _jobs = new();
        public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_jobs.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadJob>>(_jobs.ToArray());
        public Task SaveAsync(DownloadJob job, CancellationToken ct) { _jobs.RemoveAll(x => x.Id == job.Id); _jobs.Add(job); return Task.CompletedTask; }
        public Task<IReadOnlyList<FileState>> GetByJobAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<FileState>>(Files.Where(x => x.JobId == id).ToArray());
        public Task SaveAsync(FileState state, CancellationToken ct) { Files.RemoveAll(x => x.Id == state.Id); Files.Add(state); return Task.CompletedTask; }
        public Task<ProviderAccount?> GetAsync(string provider, string label, CancellationToken ct) => Task.FromResult<ProviderAccount?>(null);
        Task<IReadOnlyList<ProviderAccount>> NugsDownloader.App.Abstractions.ICredentialStore.ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProviderAccount>>(Array.Empty<ProviderAccount>());
        public Task SaveAsync(ProviderAccount account, CancellationToken ct) => Task.CompletedTask;
        public Task<string> StoreAsync(string provider, string label, string secret, CancellationToken ct) => Task.FromResult("fixture-secret-ref");
        public Task<string?> GetAsync(string reference, CancellationToken ct) => Task.FromResult<string?>(null);
        public Task DeleteAsync(string reference, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FailingContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(new byte[] { 1, 2, 3 });
            throw new IOException("Fixture transfer interrupted.");
        }
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class InlineProgress : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) { }
    }
}
