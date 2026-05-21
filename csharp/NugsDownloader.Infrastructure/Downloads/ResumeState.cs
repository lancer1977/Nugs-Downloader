namespace NugsDownloader.Infrastructure.Downloads;

public sealed record ResumeState(
    string FilePath,
    string Url,
    long TotalSize,
    long DownloadedSize,
    string? ETag,
    string? Checksum,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
