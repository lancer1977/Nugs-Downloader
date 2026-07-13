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
            if (url.Contains("method=catalog.containersAll") && url.Contains("startOffset=1"))
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

            if (url.Contains("method=catalog.containersAll"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        response = new
                        {
                            containers = Array.Empty<object>()
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
    public async Task BuildDownloadPlanAsync_UsesAlbumNamingAndOutputRoot()
    {
        var provider = CreateProvider();
        var discovery = CreateDiscovery(
            "Album",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song 1", "audio", 0, new Dictionary<string, string>())
            },
            artistName: "Artist");

        var preferences = new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true);
        var plan = await provider.BuildDownloadPlanAsync(discovery, preferences, CancellationToken.None);

        Assert.Equal("nugs", plan.ProviderId);
        Assert.Equal("Downloads", plan.OutputRoot);
        Assert.Single(plan.Items);
        Assert.Equal("flac", plan.Items[0].Metadata["selectedAudioFormat"]);
        Assert.Equal("1", plan.Items[0].Metadata["trackNumber"]);
        Assert.Equal(Path.Combine("Downloads", "Artist", "Album", "01. Song 1.flac"), Assert.Single(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Audio).FilePath);
        Assert.Equal(Path.Combine("Downloads", "Artist", "Album", "metadata.nfo"), Assert.Single(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Metadata).FilePath);
        Assert.Equal(Path.Combine("Downloads", "Artist", "Album", "cover.jpg"), Assert.Single(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Artwork).FilePath);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_UsesPlaylistNamingAndOutputRoot()
    {
        var provider = CreateProvider();
        var discovery = CreateDiscovery(
            "Playlist",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("20", "Playlist Song", "audio", 0, new Dictionary<string, string>())
            },
            artistName: null,
            metadata: new Dictionary<string, string> { ["playlistId"] = "321" });

        var preferences = new NugsDownloader.Domain.ValueObjects.DownloadPreferences("aac", "1080p", false, false, false, "Library", true, true);
        var plan = await provider.BuildDownloadPlanAsync(discovery, preferences, CancellationToken.None);

        var audioFile = Assert.Single(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Audio);
        Assert.Equal(Path.Combine("Library", "Playlist", "01. Playlist Song.aac"), audioFile.FilePath);
        Assert.Equal("aac", plan.Items[0].Metadata["selectedAudioFormat"]);
        Assert.Equal("1", plan.Items[0].Metadata["trackNumber"]);
        Assert.All(plan.ExpectedFiles, file => Assert.StartsWith("Library", file.FilePath));
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_UsesVideoNamingAndOutputRoot()
    {
        var provider = CreateProvider();
        var discovery = CreateDiscovery(
            "Concert",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("v1", "Main Feature", "video", 0, new Dictionary<string, string>())
            },
            artistName: null,
            hasVideo: true,
            hasAudio: false);

        var preferences = new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "4k", false, false, false, "Videos", true, true);
        var plan = await provider.BuildDownloadPlanAsync(discovery, preferences, CancellationToken.None);

        var videoFile = Assert.Single(plan.ExpectedFiles, file => file.Kind == NugsDownloader.Domain.Entities.FileKind.Video);
        Assert.Equal(Path.Combine("Videos", "Concert", "Concert_4k.mp4"), videoFile.FilePath);
        Assert.Equal("4k", plan.Items[0].Metadata["selectedVideoResolution"]);
        Assert.Equal("1", plan.Items[0].Metadata["trackNumber"]);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_SkipsVideoAndLivestreamItemsWhenRequested()
    {
        var provider = CreateProvider();
        var discovery = CreateDiscovery(
            "Album",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>()),
                new NugsDownloader.Domain.ValueObjects.MediaItem("2", "Video", "video", 1, new Dictionary<string, string>()),
                new NugsDownloader.Domain.ValueObjects.MediaItem("3", "Watch", "livestream", 2, new Dictionary<string, string>())
            });

        var plan = await provider.BuildDownloadPlanAsync(discovery, new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", true, false, false, "Downloads", true, true), CancellationToken.None);

        Assert.Single(plan.Items);
        Assert.All(plan.Items, item => Assert.Equal("audio", item.Kind));
        Assert.Equal(3, plan.ExpectedFiles.Count);
        Assert.Equal(NugsDownloader.Domain.Entities.FileKind.Audio, plan.ExpectedFiles[0].Kind);
    }

    [Fact]
    public async Task BuildDownloadPlanAsync_ForcesVideoOrLivestreamSelectionWhenRequested()
    {
        var provider = CreateProvider();
        var discovery = CreateDiscovery(
            "Watch Party",
            new[]
            {
                new NugsDownloader.Domain.ValueObjects.MediaItem("1", "Song", "audio", 0, new Dictionary<string, string>()),
                new NugsDownloader.Domain.ValueObjects.MediaItem("2", "Live Set", "livestream", 1, new Dictionary<string, string>())
            },
            hasVideo: true,
            hasAudio: true);

        var plan = await provider.BuildDownloadPlanAsync(discovery, new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "4k", false, false, true, "Downloads", true, true), CancellationToken.None);

        Assert.Single(plan.Items);
        Assert.Equal("livestream", plan.Items[0].Kind);
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

        var progress = new RecordingProgress<NugsDownloader.Domain.ValueObjects.DownloadProgress>();
        await provider.ExecuteDownloadAsync(plan, progress, CancellationToken.None);

        Assert.Single(progress.Reports);
        Assert.Equal("nugs", progress.Reports[0].ProviderId);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_WritesMediaBytesAndMetadataFiles()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "nugs-provider-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "https://assets.example.test/song.flac")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5 })
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }))));

            var audioPath = Path.Combine(tempRoot, "Artist", "Album", "01. Song.flac");
            var metadataPath = Path.Combine(tempRoot, "Artist", "Album", "metadata.nfo");
            var plan = new NugsDownloader.Domain.ValueObjects.DownloadPlan(
                "nugs",
                Guid.NewGuid(),
                new[]
                {
                    new NugsDownloader.Domain.ValueObjects.MediaItem(
                        "1",
                        "Song",
                        "audio",
                        0,
                        new Dictionary<string, string>
                        {
                            ["clipUrl"] = "https://assets.example.test/song.flac"
                        })
                },
                tempRoot,
                new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, tempRoot, true, false),
                new[]
                {
                    new NugsDownloader.Domain.Entities.FileState(Guid.NewGuid(), Guid.Empty, audioPath, NugsDownloader.Domain.Entities.FileKind.Audio, NugsDownloader.Domain.Entities.FileStatus.Partial, 0, 0, null, null),
                    new NugsDownloader.Domain.Entities.FileState(Guid.NewGuid(), Guid.Empty, metadataPath, NugsDownloader.Domain.Entities.FileKind.Metadata, NugsDownloader.Domain.Entities.FileStatus.Partial, 0, 0, null, null)
                },
                new Dictionary<string, string>());

            var progress = new RecordingProgress<NugsDownloader.Domain.ValueObjects.DownloadProgress>();
            await provider.ExecuteDownloadAsync(plan, progress, CancellationToken.None);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await File.ReadAllBytesAsync(audioPath));
            Assert.False(File.Exists(audioPath + ".partial"));
            Assert.Contains("Nugs download metadata", await File.ReadAllTextAsync(metadataPath));
            Assert.False(File.Exists(metadataPath + ".partial"));
            Assert.Equal(2, progress.Reports.Count);
            Assert.Contains("Downloaded", progress.Reports.Last().Message);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteDownloadAsync_ResolvesEntitledFullQualityUrlInsteadOfCatalogClip()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "nugs-provider-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var requests = new List<Uri>();
            var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(request =>
            {
                var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
                if (url.Contains("connect/token", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "test-access-token" }) });
                }

                if (url.Contains("connect/userinfo", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { sub = "user-789" }) });
                }

                if (url.Contains("me/subscriptions", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
                    {
                        plan = new { description = "Subscriber", planID = "plan-123" },
                        promo = new { plan = new { description = "", planID = "" } },
                        legacySubscriptionID = "subscription-456",
                        startedAt = "01/02/2026 03:04:05",
                        endsAt = "12/31/2026 23:59:59",
                        isContentAccessible = true,
                        productFormatList = Array.Empty<object>()
                    }) });
                }

                if (url.Contains("bigriver/subPlayer.aspx", StringComparison.Ordinal))
                {
                    requests.Add(request.RequestUri!);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { streamLink = "https://assets.example.test/full-quality.flac?sig=test-signature" }) });
                }

                if (url.Contains("full-quality.flac", StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 9, 8, 7 }) });
                }

                if (url.Contains("catalog-clip", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Catalog clips must not be downloaded for entitled audio.");
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }))));

            var auth = await provider.AuthenticateAsync(new NugsDownloader.Domain.ValueObjects.Credentials("user", "password", null, "label"), CancellationToken.None);
            Assert.True(auth.Success);

            var audioPath = Path.Combine(tempRoot, "full-quality.flac");
            var plan = new NugsDownloader.Domain.ValueObjects.DownloadPlan(
                "nugs",
                Guid.NewGuid(),
                new[] { new NugsDownloader.Domain.ValueObjects.MediaItem("12345", "Song", "audio", 0, new Dictionary<string, string> { ["clipUrl"] = "https://assets.example.test/catalog-clip.m4a", ["selectedAudioFormat"] = "flac" }) },
                tempRoot,
                new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, tempRoot, false, false),
                new[] { new NugsDownloader.Domain.Entities.FileState(Guid.NewGuid(), Guid.Empty, audioPath, NugsDownloader.Domain.Entities.FileKind.Audio, NugsDownloader.Domain.Entities.FileStatus.Partial, 0, 0, null, null) },
                null);

            await provider.ExecuteDownloadAsync(plan, new RecordingProgress<NugsDownloader.Domain.ValueObjects.DownloadProgress>(), CancellationToken.None);

            Assert.Equal(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(audioPath));
            Assert.Single(requests);
            Assert.Contains("platformID=1", requests[0].Query, StringComparison.Ordinal);
            Assert.Contains("trackID=12345", requests[0].Query, StringComparison.Ordinal);
            Assert.False(File.Exists(audioPath + ".partial"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteDownloadAsync_ThrowsWhenMediaDownloadIsEmptyAndRemovesPartialFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "nugs-provider-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var provider = new NugsMediaProvider(new NugsApiClient(new HttpClient(new FakeHttpHandler(request =>
            {
                if (request.RequestUri?.AbsoluteUri == "https://assets.example.test/empty.flac")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Array.Empty<byte>())
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }))));

            var audioPath = Path.Combine(tempRoot, "empty.flac");
            var plan = new NugsDownloader.Domain.ValueObjects.DownloadPlan(
                "nugs",
                Guid.NewGuid(),
                new[]
                {
                    new NugsDownloader.Domain.ValueObjects.MediaItem(
                        "1",
                        "Empty",
                        "audio",
                        0,
                        new Dictionary<string, string> { ["clipUrl"] = "https://assets.example.test/empty.flac" })
                },
                tempRoot,
                new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "1080p", false, false, false, tempRoot, false, false),
                new[]
                {
                    new NugsDownloader.Domain.Entities.FileState(Guid.NewGuid(), Guid.Empty, audioPath, NugsDownloader.Domain.Entities.FileKind.Audio, NugsDownloader.Domain.Entities.FileStatus.Partial, 0, 0, null, null)
                },
                null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.ExecuteDownloadAsync(plan, new Progress<NugsDownloader.Domain.ValueObjects.DownloadProgress>(), CancellationToken.None));

            Assert.False(File.Exists(audioPath));
            Assert.False(File.Exists(audioPath + ".partial"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = new();

        public void Report(T value) => Reports.Add(value);
    }

    private static NugsMediaProvider CreateProvider() =>
        new(new NugsApiClient(new HttpClient(new FakeHttpHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))));

    private static NugsDownloader.Domain.ValueObjects.MediaDiscoveryResult CreateDiscovery(
        string title,
        IReadOnlyList<NugsDownloader.Domain.ValueObjects.MediaItem> items,
        string? artistName = null,
        bool? hasVideo = null,
        bool? hasAudio = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var actualHasVideo = hasVideo ?? items.Any(item => item.Kind is "video" or "livestream");
        var actualHasAudio = hasAudio ?? items.Any(item => item.Kind == "audio");

        return new NugsDownloader.Domain.ValueObjects.MediaDiscoveryResult(
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            new Uri("https://play.nugs.net/release/123"),
            title,
            artistName,
            items,
            actualHasVideo,
            actualHasAudio,
            metadata ?? new Dictionary<string, string>());
    }
}
