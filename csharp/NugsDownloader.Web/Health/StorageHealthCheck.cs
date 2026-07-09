using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NugsDownloader.Web.Options;

namespace NugsDownloader.Web.Health;

public sealed class StorageHealthCheck : IHealthCheck
{
    private readonly NugsDownloaderStorageOptions _options;

    public StorageHealthCheck(IOptions<NugsDownloaderStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await ProbeDirectoryAsync(_options.GetStateDirectory(), cancellationToken);
            await ProbeDirectoryAsync(_options.GetDownloadDirectory(), cancellationToken);
            var availableBytes = GetAvailableBytes(_options.GetDownloadDirectory());
            if (availableBytes < _options.MinimumFreeBytes)
            {
                return HealthCheckResult.Unhealthy(
                    $"Download storage free space is below threshold: {availableBytes} bytes available, {_options.MinimumFreeBytes} required.");
            }

            return HealthCheckResult.Healthy(
                $"State and download directories are writable; download storage has {availableBytes} bytes available.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("State or download storage is not writable.", ex);
        }
    }

    private static async Task ProbeDirectoryAsync(string directory, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var probe = Path.Combine(directory, $".nugs-health-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(probe, DateTimeOffset.UtcNow.ToString("O"), ct);
        File.Delete(probe);
    }

    private static long GetAvailableBytes(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (string.IsNullOrWhiteSpace(root))
        {
            return long.MaxValue;
        }

        return new DriveInfo(root).AvailableFreeSpace;
    }
}
