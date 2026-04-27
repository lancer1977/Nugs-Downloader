namespace NugsDownloader.Domain.ValueObjects;

public sealed record MediaItem(
    string Id,
    string DisplayName,
    string Kind,
    int Index,
    IReadOnlyDictionary<string, string> Metadata);

