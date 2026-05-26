using NugsDownloader.Infrastructure.Downloads;

using Xunit;

namespace NugsDownloader.Tests;

public class FileResumeManagerTests
{
    [Fact]
    public void CreateInitialState_UsesCurrentTimestamps()
    {
        var state = FileResumeManager.CreateInitialState(
            "/tmp/audio.flac",
            "https://cdn.nugs.net/audio.flac",
            totalSize: 12_000,
            etag: "etag-1");

        Assert.Equal("/tmp/audio.flac", state.FilePath);
        Assert.Equal("https://cdn.nugs.net/audio.flac", state.Url);
        Assert.Equal(12_000, state.TotalSize);
        Assert.Equal("etag-1", state.ETag);
        Assert.Equal(0, state.DownloadedSize);
        Assert.True(state.CreatedAt <= state.UpdatedAt);
        Assert.Equal(state.CreatedAt, state.UpdatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ValidatePartialDownload_ThrowsWhenFileIsMissing()
    {
        var state = new ResumeState(
            "/tmp/missing-file.flac",
            "https://cdn.nugs.net/missing.flac",
            100,
            10,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => FileResumeManager.ValidatePartialDownload(state));
    }
}
