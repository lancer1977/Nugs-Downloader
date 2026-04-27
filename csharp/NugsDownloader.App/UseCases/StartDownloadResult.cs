namespace NugsDownloader.App.UseCases;

public sealed record StartDownloadResult(
    Guid JobId,
    string ProviderId,
    string Status,
    string Message);

