using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NugsDownloader.Web.Health;
using NugsDownloader.Web.Options;
using Xunit;

namespace NugsDownloader.Tests;

public class StorageHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenDirectoriesAreWritable()
    {
        var root = Path.Combine(Path.GetTempPath(), "nugs-health-check", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new NugsDownloaderStorageOptions
        {
            StateDirectory = Path.Combine(root, "state"),
            DownloadDirectory = Path.Combine(root, "downloads")
        });

        var health = new StorageHealthCheck(options);
        var result = await health.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("State and download directories are writable", result.Description);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyIfWritableProbeFails()
    {
        var options = Options.Create(new NugsDownloaderStorageOptions
        {
            StateDirectory = Path.Combine(Path.GetTempPath(), "nugs-no-write", Guid.NewGuid().ToString("N"))
        });

        var health = new StorageHealthCheck(options);
        var result = await health.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
