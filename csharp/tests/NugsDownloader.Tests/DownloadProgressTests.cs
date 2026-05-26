using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class DownloadProgressTests
{
    [Fact]
    public void DownloadProgress_StoresValues()
    {
        var progress = new DownloadProgress(
            Guid.NewGuid(),
            "nugs",
            512,
            1024,
            50,
            2,
            10,
            "halfway");

        Assert.Equal(50, progress.PercentComplete);
        Assert.Equal(2, progress.CurrentItem);
        Assert.Equal(10, progress.TotalItems);
        Assert.Equal("halfway", progress.Message);
    }

    [Fact]
    public void DownloadProgress_WithReturnsUpdatedCopy()
    {
        var progress = new DownloadProgress(
            Guid.NewGuid(),
            "nugs",
            200,
            1000,
            20,
            1,
            5,
            null);

        var updated = progress with { PercentComplete = 21, Message = "running" };

        Assert.Equal(21, updated.PercentComplete);
        Assert.Equal("running", updated.Message);
        Assert.Equal(progress.CurrentItem, updated.CurrentItem);
    }
}
