using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NugsDownloader.Web.Health;
using NugsDownloader.Web.Options;
using Xunit;

namespace NugsDownloader.Tests;

public class StorageHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenStorageIsWritableAndAboveFreeSpaceThreshold()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nugs-health-{Guid.NewGuid():N}");
        var options = Options.Create(new NugsDownloaderStorageOptions
        {
            StateDirectory = Path.Combine(root, "state"),
            DownloadDirectory = Path.Combine(root, "downloads"),
            MinimumFreeBytes = 1
        });
        var check = new StorageHealthCheck(options);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("bytes available", result.Description);
        Assert.True(Directory.Exists(options.Value.GetStateDirectory()));
        Assert.True(Directory.Exists(options.Value.GetDownloadDirectory()));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWhenFreeSpaceIsBelowThreshold()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nugs-health-{Guid.NewGuid():N}");
        var options = Options.Create(new NugsDownloaderStorageOptions
        {
            StateDirectory = Path.Combine(root, "state"),
            DownloadDirectory = Path.Combine(root, "downloads"),
            MinimumFreeBytes = long.MaxValue
        });
        var check = new StorageHealthCheck(options);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("free space is below threshold", result.Description);

        Directory.Delete(root, recursive: true);
    }
}
