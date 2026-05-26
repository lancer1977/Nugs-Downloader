using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Domain.Entities;

using Xunit;

namespace NugsDownloader.Tests;

public class DownloadPlanCopyTests
{
    [Fact]
    public void DownloadPlan_with_updates_output_root_and_preferences()
    {
        var original = new DownloadPlan(
            ProviderId: "nugs",
            JobId: Guid.Parse("33333333-2222-3333-4444-555555555555"),
            Items: Array.Empty<MediaItem>(),
            OutputRoot: "Downloads",
            Preferences: new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true),
            ExpectedFiles: Array.Empty<FileState>(),
            ResumeState: null);

        var updated = original with
        {
            OutputRoot = "Archive",
            ProviderId = "livephish"
        };

        Assert.Equal("Downloads", original.OutputRoot);
        Assert.Equal("livephish", updated.ProviderId);
        Assert.Equal("Archive", updated.OutputRoot);
        Assert.Equal(original.JobId, updated.JobId);
    }
}
