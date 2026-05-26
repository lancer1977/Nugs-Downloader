using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class MediaDiscoveryHasFlagsTests
{
    [Fact]
    public void MediaDiscoveryResult_ReportsAudioVideoFlags()
    {
        var result = new MediaDiscoveryResult(
            "nugs",
            new Uri("https://example.org/release/123"),
            new Uri("https://example.org/release/123"),
            "Release",
            null,
            Array.Empty<MediaItem>(),
            HasVideo: true,
            HasAudio: true,
            new Dictionary<string, string>());

        Assert.True(result.HasVideo);
        Assert.True(result.HasAudio);
    }

    [Fact]
    public void MediaDiscoveryResult_SupportsEmptyMetadata()
    {
        var result = new MediaDiscoveryResult(
            "livephish",
            new Uri("https://example.org/x"),
            new Uri("https://example.org/x"),
            "X",
            "",
            Array.Empty<MediaItem>(),
            HasVideo: false,
            HasAudio: false,
            new Dictionary<string, string>());

        Assert.False(result.HasVideo);
        Assert.False(result.HasAudio);
        Assert.Empty(result.Metadata);
    }
}
