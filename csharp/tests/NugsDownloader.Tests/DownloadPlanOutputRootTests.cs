using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class DownloadPlanOutputRootTests
{
    [Fact]
    public void DownloadPlan_OutputRootPropagatesForRecordingMode()
    {
        var plan = new DownloadPlan(
            "nugs",
            Guid.Parse("a3f0b2d0-3c42-4b56-8b2c-1e4d6f3d2fbb"),
            Array.Empty<NugsDownloader.Domain.ValueObjects.MediaItem>(),
            "Video Output",
            new NugsDownloader.Domain.ValueObjects.DownloadPreferences("flac", "4k", false, false, true, "Video Output", true, true),
            Array.Empty<NugsDownloader.Domain.Entities.FileState>(),
            null);

        Assert.Contains("Video", plan.OutputRoot);
    }
}
