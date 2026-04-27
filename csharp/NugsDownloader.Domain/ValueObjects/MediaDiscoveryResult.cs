namespace NugsDownloader.Domain.ValueObjects;

public sealed record MediaDiscoveryResult(
    string ProviderId,
    Uri SourceUrl,
    Uri CanonicalUrl,
    string Title,
    string? ArtistName,
    IReadOnlyList<MediaItem> Items,
    bool HasVideo,
    bool HasAudio,
    IReadOnlyDictionary<string, string> Metadata);

