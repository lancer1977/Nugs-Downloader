using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class DomainTests
{
    [Fact]
    public void DownloadJob_StoresCoreState()
    {
        var id = Guid.NewGuid();
        var job = new DownloadJob(
            id,
            "nugs",
            new Uri("https://play.nugs.net/release/123"),
            "Show",
            DownloadJobStatus.Pending,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            "Downloads");

        Assert.Equal(id, job.Id);
        Assert.Equal("nugs", job.ProviderId);
        Assert.Equal("Show", job.DisplayName);
        Assert.Equal(DownloadJobStatus.Pending, job.Status);
    }

    [Fact]
    public void ProviderCapabilities_ExposesSupportedFormats()
    {
        var caps = new ProviderCapabilities(
            true,
            true,
            true,
            true,
            true,
            true,
            new[] { "alac", "flac" },
            new[] { "720p", "1080p" });

        Assert.Contains("alac", caps.SupportedFormats);
        Assert.Contains("1080p", caps.SupportedResolutions);
    }
}
