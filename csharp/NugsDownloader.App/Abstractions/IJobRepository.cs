using NugsDownloader.Domain.Entities;

namespace NugsDownloader.App.Abstractions;

public interface IJobRepository
{
    Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct);
    Task SaveAsync(DownloadJob job, CancellationToken ct);

    async Task<bool> PrepareManualReplayAsync(Guid jobId, DateTimeOffset now, CancellationToken ct)
    {
        var job = await GetAsync(jobId, ct);
        if (job is null || job.Status is DownloadJobStatus.Cancelled or DownloadJobStatus.Completed ||
            job.LeaseExpiresAt is { } expiry && expiry > now)
        {
            return false;
        }

        await SaveAsync(job with
        {
            Status = DownloadJobStatus.Ready,
            CompletedAt = null,
            LeaseOwnerId = null,
            LeaseAcquiredAt = null,
            LeaseExpiresAt = null,
            NextAttemptAt = null
        }, ct);
        return true;
    }

    async Task<bool> SaveIfLeaseOwnedAsync(DownloadJob job, string workerId, CancellationToken ct)
    {
        var current = await GetAsync(job.Id, ct);
        if (current is null || !string.Equals(current.LeaseOwnerId, workerId, StringComparison.Ordinal))
        {
            return false;
        }

        await SaveAsync(job with
        {
            LeaseOwnerId = workerId,
            LeaseAcquiredAt = current.LeaseAcquiredAt,
            LeaseExpiresAt = current.LeaseExpiresAt
        }, ct);
        return true;
    }

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

        if (job.LeaseExpiresAt is { } expiry && expiry > acquiredAt)
        {
            return false;
        }

        if (job.NextAttemptAt is { } nextAttempt && nextAttempt > acquiredAt)
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

    async Task<bool> RenewLeaseAsync(
        Guid jobId,
        string workerId,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        var job = await GetAsync(jobId, ct);
        if (job is null || !string.Equals(job.LeaseOwnerId, workerId, StringComparison.Ordinal) ||
            job.Status is DownloadJobStatus.Cancelled or DownloadJobStatus.Completed)
        {
            return false;
        }

        await SaveAsync(job with { LeaseExpiresAt = expiresAt }, ct);
        return true;
    }
}
