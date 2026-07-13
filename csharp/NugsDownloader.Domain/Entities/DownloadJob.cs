using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Domain.Entities;

public sealed record DownloadJob(
    Guid Id,
    string ProviderId,
    Uri SourceUrl,
    string DisplayName,
    DownloadJobStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    string OutputPath,
    string? CredentialLabel = null,
    string? CredentialUsername = null,
    DownloadPreferences? Preferences = null,
    string? LeaseOwnerId = null,
    DateTimeOffset? LeaseAcquiredAt = null,
    DateTimeOffset? LeaseExpiresAt = null,
    int RetryCount = 0,
    DateTimeOffset? NextAttemptAt = null);

public enum DownloadJobStatus
{
    Pending,
    Discovering,
    Ready,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
