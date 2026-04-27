using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Domain.ValueObjects;

public sealed record DownloadPlan(
    string ProviderId,
    Guid JobId,
    IReadOnlyList<MediaItem> Items,
    string OutputRoot,
    DownloadPreferences Preferences,
    IReadOnlyList<FileState> ExpectedFiles,
    object? ResumeState);

