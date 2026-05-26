using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Domain.Entities;
using Xunit;

namespace NugsDownloader.Tests;

public class DownloadPlanTests
{
    [Fact]
    public void DownloadPlan_UsesResumeStateInRecordShape()
    {
        var plan = new DownloadPlan(
            "nugs",
            Guid.Parse("12345678-1234-1234-1234-1234567890ab"),
            Array.Empty<MediaItem>(),
            "/tmp/output",
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true),
            Array.Empty<FileState>(),
            new { Stage = "ready" });

        Assert.Equal("nugs", plan.ProviderId);
        Assert.Equal("/tmp/output", plan.OutputRoot);
        Assert.Contains("ready", plan.ResumeState?.ToString() ?? string.Empty);
    }

    [Fact]
    public void DownloadPlan_CanBeCopiedWithDifferentResumeState()
    {
        var plan = new DownloadPlan(
            "nugs",
            Guid.NewGuid(),
            Array.Empty<MediaItem>(),
            "Downloads",
            new DownloadPreferences(null, null, true, true, false, "Downloads", false, false),
            Array.Empty<FileState>(),
            null);

        var resumed = plan with { ResumeState = "resumed" };

        Assert.Null(plan.ResumeState);
        Assert.Equal("resumed", resumed.ResumeState);
        Assert.Equal(plan.OutputRoot, resumed.OutputRoot);
        Assert.Equal(plan.ProviderId, resumed.ProviderId);
    }
}
