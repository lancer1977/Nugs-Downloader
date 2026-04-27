using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.App.UseCases;

public sealed record StartDownloadRequest(
    Guid? JobId,
    string? ProviderId,
    Uri SourceUrl,
    Credentials Credentials,
    DownloadPreferences Preferences);
