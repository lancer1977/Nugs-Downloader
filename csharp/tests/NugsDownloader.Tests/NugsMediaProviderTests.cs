using System.Net;
using System.Net.Http.Json;
using NugsDownloader.Infrastructure.Providers.Nugs;
using Xunit;

namespace NugsDownloader.Tests;

public class NugsMediaProviderTests
{
    [Fact]
    public async Task AuthenticateAsync_ReturnsSuccessWithToken()
    {
        using var client = new HttpClient(new FakeHttpHandler(async request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("connect/token") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "token-123" })
                };
            }

            if (request.RequestUri?.AbsoluteUri.Contains("connect/userinfo") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { sub = "user-1" })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var provider = new NugsMediaProvider(new NugsApiClient(client));
        var result = await provider.AuthenticateAsync(new NugsDownloader.Domain.ValueObjects.Credentials("user", "pass", null, "label"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("token-123", result.SecretRef);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsAlbumItems()
    {
        using var client = new HttpClient(new FakeHttpHandler(async request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("api.aspx") && url.Contains("method=catalog.container"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        response = new
                        {
                            artistName = "Artist",
                            containerInfo = "Album",
                            containerTypeStr = "album",
                            tracks = new[]
                            {
                                new { trackID = 1, songTitle = "Song 1" },
                                new { trackID = 2, songTitle = "Song 2" }
                            }
                        }
                    })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var provider = new NugsMediaProvider(new NugsApiClient(client));
        var discovery = await provider.DiscoverAsync(new Uri("https://play.nugs.net/release/123"), CancellationToken.None);

        Assert.Equal("Album", discovery.Title);
        Assert.Equal(2, discovery.Items.Count);
        Assert.Equal("audio", discovery.Items[0].Kind);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsArtistItems()
    {
        using var client = new HttpClient(new FakeHttpHandler(async request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("method=catalog.containersAll"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        response = new
                        {
                            containers = new[]
                            {
                                new
                                {
                                    artistName = "Artist",
                                    tracks = new[]
                                    {
                                        new { trackID = 10, songTitle = "Track A" }
                                    }
                                }
                            }
                        }
                    })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var provider = new NugsMediaProvider(new NugsApiClient(client));
        var discovery = await provider.DiscoverAsync(new Uri("https://play.nugs.net/artist/987"), CancellationToken.None);

        Assert.Equal("Artist", discovery.Title);
        Assert.Single(discovery.Items);
        Assert.Equal("audio", discovery.Items[0].Kind);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsPlaylistItems()
    {
        using var client = new HttpClient(new FakeHttpHandler(async request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("method=catalog.playlist"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        response = new
                        {
                            playListName = "Playlist",
                            items = new[]
                            {
                                new { track = new { trackID = 20, songTitle = "Song X" } }
                            }
                        }
                    })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var provider = new NugsMediaProvider(new NugsApiClient(client));
        var discovery = await provider.DiscoverAsync(new Uri("https://play.nugs.net/playlists/321"), CancellationToken.None);

        Assert.Equal("Playlist", discovery.Title);
        Assert.Single(discovery.Items);
        Assert.Equal("audio", discovery.Items[0].Kind);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsVideoItemForVideoUrls()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));

        var discovery = await provider.DiscoverAsync(new Uri("https://play.nugs.net/videos/456"), CancellationToken.None);

        Assert.True(discovery.HasVideo);
        Assert.Single(discovery.Items);
        Assert.Equal("video", discovery.Items[0].Kind);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsLivestreamItemForWatchUrls()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));

        var discovery = await provider.DiscoverAsync(new Uri("https://play.nugs.net/watch/999"), CancellationToken.None);

        Assert.True(discovery.HasVideo);
        Assert.Single(discovery.Items);
        Assert.Equal("livestream", discovery.Items[0].Kind);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_MapsExpectedFiles()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
        var discovery = new NugsDownloader.Domain.ValueObjects.MediaDiscoveryResult(
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            new Uri("https://play.nugs.net/release/123"),
            "Album",
            "Artist",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>()),
                new NugsDownloader.Domain.ValueObjects.MediaItem("2", "Video", "video", 1, new Dictionary<string, string>())
            },
            true,
            true,
            new Dictionary<string, string>());

        var plan = await provider.BuildDownloadPlanAsync(discovery, new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true), CancellationToken.None);

        Assert.Equal("nugs", plan.ProviderId);
        Assert.Equal(4, plan.ExpectedFiles.Count);
        Assert.Contains(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Video);
        Assert.Contains(plan.Items, item => item.Metadata["selectedAudioFormat"] == "flac");
        Assert.Contains(plan.ExpectedFiles, file => file.FilePath.Contains("flac", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("Downloads", plan.ExpectedFiles[0].FilePath);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_SkipsVideoItemsWhenRequested()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
        var discovery = new NugsDownloader.Domain.ValueObjects.MediaDiscoveryResult(
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            new Uri("https://play.nugs.net/release/123"),
            "Album",
            "Artist",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>()),
                new NugsDownloader.Domain.ValueObjects.MediaItem("2", "Video", "video", 1, new Dictionary<string, string>())
            },
            true,
            true,
            new Dictionary<string, string>());

        var plan = await provider.BuildDownloadPlanAsync(discovery, new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", true, false, false, "Downloads", true, true), CancellationToken.None);

        Assert.Single(plan.Items);
        Assert.All(plan.Items, item => Assert.Equal("audio", item.Kind));
        Assert.Equal(3, plan.ExpectedFiles.Count);
        Assert.Equal(NugsDownloader.Domain.Entities.FileKind.Audio, plan.ExpectedFiles[0].Kind);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_PrefersVideoItemsWhenForced()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
        var discovery = new NugsDownloader.Domain.ValueObjects.MediaDiscoveryResult(
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            new Uri("https://play.nugs.net/release/123"),
            "Album",
            "Artist",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>()),
                new NugsDownloader.Domain.ValueObjects.MediaItem("2", "Video", "video", 1, new Dictionary<string, string>())
            },
            true,
            true,
            new Dictionary<string, string>());

        var plan = await provider.BuildDownloadPlanAsync(discovery, new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "4k", false, false, true, "Downloads", true, true), CancellationToken.None);

        Assert.Single(plan.Items);
        Assert.All(plan.Items, item => Assert.Equal("video", item.Kind));
        Assert.Equal("4k", plan.Items[0].Metadata["selectedVideoResolution"]);
        Assert.Equal(3, plan.ExpectedFiles.Count);
        Assert.Contains(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Video);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_AddsMetadataAndArtworkFilesWhenEnabled()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
        var discovery = new NugsDownloader.Domain.ValueObjects.MediaDiscoveryResult(
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            new Uri("https://play.nugs.net/release/123"),
            "Album",
            "Artist",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>())
            },
            false,
            true,
            new Dictionary<string, string>());

        var plan = await provider.BuildDownloadPlanAsync(discovery, new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true), CancellationToken.None);

        Assert.Contains(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Metadata && file.FilePath.EndsWith("metadata.nfo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Artwork && file.FilePath.EndsWith("cover.jpg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteDownloadAsync_ReportsProgress()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));

        var plan = new NugsDownloader.Domain.ValueObjects.DownloadPlan(
            "nugs",
            Guid.NewGuid(),
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>())
            },
            "Downloads",
            new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true),
            Array.Empty<NugsDownloader.Domain.Entities.FileState>(),
            null);

        var reports = new List<NugsDownloader.Domain.ValueObjects.DownloadProgress>();
        await provider.ExecuteDownloadAsync(plan, new Progress<NugsDownloader.Domain.ValueObjects.DownloadProgress>(reports.Add), CancellationToken.None);

        Assert.Single(reports);
        Assert.Equal("nugs", reports[0].ProviderId);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_ThrowsWhenCancelled()
    {
        var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));
        var plan = new NugsDownloader.Domain.ValueObjects.DownloadPlan(
            "nugs",
            Guid.NewGuid(),
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>())
            },
            "Downloads",
            new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true),
            Array.Empty<NugsDownloader.Domain.Entities.FileState>(),
            null);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.ExecuteDownloadAsync(plan, new Progress<NugsDownloader.Domain.ValueObjects.DownloadProgress>(), cts.Token));
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request);
    }
}
