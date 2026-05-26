using NugsDownloader.Domain.ValueObjects;

using Xunit;

namespace NugsDownloader.Tests;

public class DownloadProgressRecordTests
{
    [Fact]
    public void DownloadProgress_StoresPercentAndMessage()
    {
        var progress = new DownloadProgress(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "nugs",
            DownloadedBytes: 4_000,
            TotalBytes: 5_000,
            PercentComplete: 80,
            CurrentItem: 2,
            TotalItems: 8,
            Message: "halfway");

        Assert.Equal(80, progress.PercentComplete);
        Assert.Equal("halfway", progress.Message);
        Assert.Equal(2, progress.CurrentItem);
        Assert.Equal(8, progress.TotalItems);
    }

    [Fact]
    public void DownloadProgress_WithReturnsUpdatedCopy()
    {
        var original = new DownloadProgress(
            Guid.NewGuid(),
            "livephish",
            5,
            10,
            PercentComplete: 50,
            CurrentItem: 1,
            TotalItems: 3,
            Message: "copy");

        var updated = original with
        {
            PercentComplete = 75,
            Message = "copied"
        };

        Assert.Equal("copy", original.Message);
        Assert.Equal("copied", updated.Message);
        Assert.Equal(75, updated.PercentComplete);
    }
}
