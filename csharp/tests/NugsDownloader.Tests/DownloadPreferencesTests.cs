using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class DownloadPreferencesTests
{
    [Fact]
    public void DownloadPreferences_StoresQualityPreferences()
    {
        var prefs = new DownloadPreferences(
            PreferredAudioFormat: "flac",
            PreferredVideoResolution: "4k",
            SkipVideos: false,
            SkipChapters: true,
            ForceVideo: false,
            OutputRoot: "Music",
            WriteMetadata: true,
            WriteArtwork: true);

        Assert.Equal("flac", prefs.PreferredAudioFormat);
        Assert.Equal("4k", prefs.PreferredVideoResolution);
        Assert.True(prefs.SkipChapters);
        Assert.True(prefs.WriteMetadata);
        Assert.Equal("Music", prefs.OutputRoot);
    }

    [Fact]
    public void DownloadPreferences_CanRepresentVideoForcingBehavior()
    {
        var prefs = new DownloadPreferences(
            PreferredAudioFormat: null,
            PreferredVideoResolution: "1080p",
            SkipVideos: false,
            SkipChapters: false,
            ForceVideo: true,
            OutputRoot: "Videos",
            WriteMetadata: false,
            WriteArtwork: false);

        Assert.True(prefs.ForceVideo);
        Assert.Equal("Videos", prefs.OutputRoot);
        Assert.False(prefs.WriteArtwork);
    }
}
