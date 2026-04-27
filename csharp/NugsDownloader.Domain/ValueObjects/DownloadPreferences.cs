namespace NugsDownloader.Domain.ValueObjects;

public sealed record DownloadPreferences(
    string? PreferredAudioFormat,
    string? PreferredVideoResolution,
    bool SkipVideos,
    bool SkipChapters,
    bool ForceVideo,
    string OutputRoot,
    bool WriteMetadata,
    bool WriteArtwork);

