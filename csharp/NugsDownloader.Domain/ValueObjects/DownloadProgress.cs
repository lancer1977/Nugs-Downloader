namespace NugsDownloader.Domain.ValueObjects;

public sealed record DownloadProgress(
    Guid JobId,
    string ProviderId,
    long DownloadedBytes,
    long TotalBytes,
    int PercentComplete,
    int CurrentItem,
    int TotalItems,
    string? Message);

