using NugsDownloader.Infrastructure.Downloads;
using Xunit;

namespace NugsDownloader.Tests;

public class ResumeStateTests
{
    [Fact]
    public void ResumeState_TracksTimestampsAndProgress()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var updatedAt = DateTimeOffset.UtcNow;

        var state = new ResumeState(
            "/tmp/file.bin",
            "https://example.org/file.bin",
            TotalSize: 10_000,
            DownloadedSize: 5_000,
            ETag: "etag-1",
            Checksum: "abcd",
            createdAt,
            updatedAt);

        Assert.Equal("/tmp/file.bin", state.FilePath);
        Assert.Equal("etag-1", state.ETag);
        Assert.Equal(10_000, state.TotalSize);
        Assert.Equal(5_000, state.DownloadedSize);
        Assert.Equal(createdAt, state.CreatedAt);
        Assert.Equal(updatedAt, state.UpdatedAt);
    }
}
