using NugsDownloader.App.UseCases;

using Xunit;

namespace NugsDownloader.Tests;

public class StartDownloadResultCopyTests
{
    [Fact]
    public void StartDownloadResult_UsesPositionalArguments()
    {
        var result = new StartDownloadResult(
            Guid.Parse("aaaaaaa1-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "nugs",
            "Queued",
            "starting");

        Assert.Equal("Queued", result.Status);
        Assert.Equal("starting", result.Message);
    }

    [Fact]
    public void StartDownloadResult_WithUpdatesResultState()
    {
        var result = new StartDownloadResult(
            Guid.Parse("bbbbbbb1-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "nugs",
            "Queued",
            "starting");

        var updated = result with { Status = "Ready", Message = "ok" };

        Assert.Equal("Queued", result.Status);
        Assert.Equal("Ready", updated.Status);
        Assert.Equal("starting", result.Message);
        Assert.Equal("ok", updated.Message);
    }
}
