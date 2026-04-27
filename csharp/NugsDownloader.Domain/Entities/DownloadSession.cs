namespace NugsDownloader.Domain.Entities;

public sealed record DownloadSession(
    Guid Id,
    Guid JobId,
    string ProviderId,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastProgressAt,
    long DownloadedBytes,
    long TotalBytes,
    int PercentComplete,
    int CurrentItem,
    int TotalItems);

