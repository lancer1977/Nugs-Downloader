namespace NugsDownloader.Domain.ValueObjects;

public sealed record ProviderCapabilities(
    bool SupportsAudio,
    bool SupportsVideo,
    bool SupportsChapters,
    bool SupportsResume,
    bool SupportsTokens,
    bool SupportsPasswordLogin,
    IReadOnlyList<string> SupportedFormats,
    IReadOnlyList<string> SupportedResolutions);

