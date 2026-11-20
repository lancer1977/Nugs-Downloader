using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Domain.ValueObjects;

public sealed record DownloadPlan(
    string ProviderId,
    Guid JobId,
    IReadOnlyList<MediaItem> Items,
    string OutputRoot,
    DownloadPreferences Preferences,
    IReadOnlyList<FileState> ExpectedFiles,
    object? ResumeState)
{
    // Execution may resolve a different container than the provisional planned path.
    // The workflow must persist these resolved paths, retaining the original file IDs.
    public IReadOnlyList<FileState> ExpectedFiles { get; set; } = ExpectedFiles;
}

