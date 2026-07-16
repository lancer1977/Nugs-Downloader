using NugsDownloader.Domain.Entities;

namespace NugsDownloader.App.Abstractions;

public interface IJobRepository
{
    Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct);
    Task SaveAsync(DownloadJob job, CancellationToken ct);

    async Task<bool> TryAcquireLeaseAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset acquiredAt,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        var job = await GetAsync(jobId, ct);
        if (job is null || job.Status is DownloadJobStatus.Cancelled or DownloadJobStatus.Completed)
        {
            return false;
        }

        if (job.LeaseExpiresAt is { } expiry && expiry > acquiredAt &&
            !string.Equals(job.LeaseOwnerId, workerId, StringComparison.Ordinal))
        {
            return false;
        }

        await SaveAsync(job with
        {
            LeaseOwnerId = workerId,
            LeaseAcquiredAt = acquiredAt,
            LeaseExpiresAt = expiresAt,
            NextAttemptAt = null
        }, ct);
        return true;
    }
}
