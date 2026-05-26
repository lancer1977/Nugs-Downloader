using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class ProviderCapabilitiesAdditionalTests
{
    [Fact]
    public void ProviderCapabilities_RecordsCanBeCopiedWithChanges()
    {
        var baseCapabilities = new ProviderCapabilities(
            SupportsAudio: true,
            SupportsVideo: false,
            SupportsChapters: false,
            SupportsResume: false,
            SupportsTokens: false,
            SupportsPasswordLogin: false,
            SupportedFormats: new[] { "flac" },
            SupportedResolutions: new[] { "1080p" });

        var overrideCapabilities = baseCapabilities with { SupportsVideo = true };

        Assert.True(baseCapabilities.SupportsAudio);
        Assert.False(baseCapabilities.SupportsVideo);
        Assert.True(overrideCapabilities.SupportsVideo);
        Assert.Equal(baseCapabilities.SupportedFormats, overrideCapabilities.SupportedFormats);
    }
}
