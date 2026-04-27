namespace NugsDownloader.Domain.Entities;

public sealed record FileState(
    Guid Id,
    Guid JobId,
    string FilePath,
    FileKind Kind,
    FileStatus Status,
    long ExpectedSize,
    long ActualSize,
    string? Checksum,
    DateTimeOffset? LastVerifiedAt);

public enum FileKind
{
    Audio,
    Video,
    Metadata,
    Artwork
}

public enum FileStatus
{
    Missing,
    Partial,
    Complete,
    Stale,
    Corrupt
}

