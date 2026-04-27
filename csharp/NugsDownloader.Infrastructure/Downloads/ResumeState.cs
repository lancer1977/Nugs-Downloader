namespace NugsDownloader.Infrastructure.Downloads;

public sealed record ResumeState(
    string FilePath,
    string Url,
    long TotalSize,
    long DownloadedSize,
    DateTimeOffset LastModified,
    string ETag,
    string Checksum,
    IReadOnlyList<SegmentState>? Segments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SegmentState(
    int Index,
    string Url,
    long Size,
    string Checksum,
    bool Completed);
