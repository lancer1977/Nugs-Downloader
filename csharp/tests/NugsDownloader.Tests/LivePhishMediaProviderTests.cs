using System.Net;
using System.Text;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Infrastructure.Providers.LivePhish;
using Xunit;

namespace NugsDownloader.Tests;

public class LivePhishMediaProviderTests
{
    [Fact]
    public void CanHandle_ReturnsTrueForLivePhishUrls()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.True(provider.CanHandle(new Uri("https://plus.livephish.com/index.html#/catalog/recording/12345")));
        Assert.True(provider.CanHandle(new Uri("https://www.livephish.com/browse/music/0,12345/phish-show")));
        Assert.False(provider.CanHandle(new Uri("https://www.notlivephish.com/browse/music/0,12345/phish-show")));
        Assert.False(provider.CanHandle(new Uri("https://play.nugs.net/release/12345")));
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsSuccessWhenTokenAndUserInfoAreAvailable()
    {
        var provider = CreateProvider(request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("/connect/token", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse("{\"access_token\":\"livephish-token\"}");
            }

            if (request.RequestUri?.AbsoluteUri.Contains("/connect/userinfo", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse("{\"sub\":\"phish-fan\"}");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await provider.AuthenticateAsync(
            new Credentials("alice", "secret", null, "LivePhish label"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("livephish", result.ProviderId);
        Assert.Equal("livephish-token", result.SecretRef);
        Assert.Equal("LivePhish label", result.DisplayName);
        Assert.Contains("phish-fan", result.Message ?? string.Empty);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsFailureWhenUsernameOrPasswordIsMissing()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await provider.AuthenticateAsync(
            new Credentials(string.Empty, string.Empty, null, "LivePhish label"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("livephish", result.ProviderId);
        Assert.Contains("Missing username or password", result.Message ?? string.Empty);
    }

    [Fact]
    public async Task DiscoverAsync_UsesRecordingMetadataAndAudioPlan()
    {
        var provider = CreateProvider(request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("method=catalog.container", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse("""
                {
                  "Response": {
                    "ArtistName": "Phish",
                    "ContainerInfo": "Madison Square Garden 2024-11-02",
                    "ContainerTypeStr": "album",
                    "Tracks": [
                      { "TrackID": 2001, "SongTitle": "Sand" },
                      { "TrackID": 2002, "SongTitle": "Ghost" }
                    ]
                  }
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var discovery = await provider.DiscoverAsync(
            new Uri("https://plus.livephish.com/index.html#/catalog/recording/12345"),
            CancellationToken.None);

        Assert.Equal("livephish", discovery.ProviderId);
        Assert.Equal("Madison Square Garden 2024-11-02", discovery.Title);
        Assert.Equal("Phish", discovery.ArtistName);
        Assert.True(discovery.HasAudio);
        Assert.False(discovery.HasVideo);
        Assert.Equal("12345", discovery.Metadata["recordingId"]);
        Assert.Equal(2, discovery.Items.Count);
        Assert.All(discovery.Items, item => Assert.Equal("audio", item.Kind));

        var plan = await provider.BuildDownloadPlanAsync(
            discovery,
            new DownloadPreferences("flac", null, false, false, false, "Downloads", true, true),
            CancellationToken.None);

        Assert.Equal("livephish", plan.ProviderId);
        Assert.Equal(2, plan.Items.Count);
        Assert.Contains(plan.ExpectedFiles, file => file.FilePath.EndsWith(Path.Combine("Phish", "Madison Square Garden 2024-11-02", "01. Sand.flac"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.ExpectedFiles, file => file.FilePath.EndsWith(Path.Combine("Phish", "Madison Square Garden 2024-11-02", "metadata.nfo"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.ExpectedFiles, file => file.FilePath.EndsWith(Path.Combine("Phish", "Madison Square Garden 2024-11-02", "cover.jpg"), StringComparison.OrdinalIgnoreCase));
        var resumeState = Assert.IsType<Dictionary<string, string>>(plan.ResumeState);
        Assert.Equal("12345", resumeState["recordingId"]);
    }

    [Fact]
    public async Task DiscoverAsync_SupportsBrowseRecordingUrls()
    {
        var provider = CreateProvider(request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("method=catalog.container", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse("""
                {
                  "Response": {
                    "ArtistName": "Phish",
                    "ContainerInfo": "Madison Square Garden 2024-11-02",
                    "ContainerTypeStr": "album",
                    "Tracks": [
                      { "TrackID": 2001, "SongTitle": "Sand" }
                    ]
                  }
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var discovery = await provider.DiscoverAsync(
            new Uri("https://www.livephish.com/browse/music/0,12345/phish-show"),
            CancellationToken.None);

        Assert.Equal("Phish", discovery.ArtistName);
        Assert.Equal("Madison Square Garden 2024-11-02", discovery.Title);
        Assert.Equal("12345", discovery.Metadata["recordingId"]);
        Assert.Single(discovery.Items);
        Assert.Equal("audio", discovery.Items[0].Kind);
    }

    [Fact]
    public async Task ExecuteDownloadAsync_ReportsProgressForTheExpectedFiles()
    {
        var provider = CreateProvider(request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("method=catalog.container", StringComparison.OrdinalIgnoreCase) == true)
            {
                return JsonResponse("""
                {
                  "Response": {
                    "ArtistName": "Phish",
                    "ContainerInfo": "Madison Square Garden 2024-11-02",
                    "ContainerTypeStr": "album",
                    "Tracks": [
                      { "TrackID": 2001, "SongTitle": "Sand" },
                      { "TrackID": 2002, "SongTitle": "Ghost" }
                    ]
                  }
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var discovery = await provider.DiscoverAsync(
            new Uri("https://plus.livephish.com/index.html#/catalog/recording/12345"),
            CancellationToken.None);

        var plan = await provider.BuildDownloadPlanAsync(
            discovery,
            new DownloadPreferences("flac", null, false, false, false, "Downloads", true, true),
            CancellationToken.None);

        var progress = new CapturingProgress();
        await provider.ExecuteDownloadAsync(plan, progress, CancellationToken.None);

        Assert.Equal(plan.ExpectedFiles.Count, progress.Reports.Count);
        Assert.Equal(100, progress.Reports.Last().PercentComplete);
        Assert.Contains("Prepared", progress.Reports.Last().Message ?? string.Empty);
    }

    private static LivePhishMediaProvider CreateProvider(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new LivePhishApiClient(new HttpClient(new FakeHttpHandler(responseFactory))));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class CapturingProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Reports { get; } = new();

        public void Report(DownloadProgress value)
        {
            Reports.Add(value);
        }
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
