using NugsDownloader.Domain.ValueObjects;

using Xunit;

namespace NugsDownloader.Tests;

public class ProviderCapabilitiesCopyTests
{
    [Fact]
    public void ProviderCapabilities_ExposesAudioVideoFlags()
    {
        var capabilities = new ProviderCapabilities(
            SupportsAudio: true,
            SupportsVideo: false,
            SupportsChapters: false,
            SupportsResume: true,
            SupportsTokens: false,
            SupportsPasswordLogin: true,
            SupportedFormats: new[] { "flac", "mp3" },
            SupportedResolutions: new[] { "720p", "1080p" });

        Assert.True(capabilities.SupportsAudio);
        Assert.False(capabilities.SupportsVideo);
        Assert.True(capabilities.SupportsResume);
        Assert.Contains("mp3", capabilities.SupportedFormats);
        Assert.Contains("1080p", capabilities.SupportedResolutions);
    }

    [Fact]
    public void ProviderCapabilities_WithCanToggleSupport()
    {
        var capabilities = new ProviderCapabilities(true, true, false, false, false, false,
            new[] { "flac" }, new[] { "1080p" });

        var downgraded = capabilities with { SupportsVideo = false, SupportsResume = true };

        Assert.True(capabilities.SupportsVideo);
        Assert.False(downgraded.SupportsVideo);
        Assert.True(downgraded.SupportsResume);
    }
}
