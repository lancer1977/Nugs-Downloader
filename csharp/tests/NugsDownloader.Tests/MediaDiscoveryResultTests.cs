using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class MediaDiscoveryResultTests
{
    [Fact]
    public void MediaDiscoveryResult_RetainsDiscoveryMetadata()
    {
        var metadata = new Dictionary<string, string>
        {
            ["genre"] = "live",
            ["artist"] = "Nugs"
        };

        var result = new MediaDiscoveryResult(
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            new Uri("https://cdn.nugs.net/release/123"),
            "Show Title",
            "Band Name",
            Array.Empty<MediaItem>(),
            HasVideo: false,
            HasAudio: true,
            metadata);

        Assert.Equal("nugs", result.ProviderId);
        Assert.Equal("Show Title", result.Title);
        Assert.True(result.HasAudio);
        Assert.False(result.HasVideo);
        Assert.Equal("Band Name", result.ArtistName);
        Assert.Equal("live", result.Metadata["genre"]);
        Assert.Equal(new Uri("https://cdn.nugs.net/release/123"), result.CanonicalUrl);
    }

    [Fact]
    public void MediaDiscoveryResult_AllowsNullArtistAndExtraTags()
    {
        var result = new MediaDiscoveryResult(
            "livephish",
            new Uri("https://example.org/recording/1"),
            new Uri("https://example.org/recording/1"),
            "Show",
            null,
            Array.Empty<MediaItem>(),
            HasVideo: false,
            HasAudio: false,
            new Dictionary<string, string>());

        Assert.Null(result.ArtistName);
        Assert.Empty(result.Items);
        Assert.Empty(result.Metadata);
    }
}
