namespace NugsDownloader.App.UseCases;

public interface IDownloadWorkflow
{
    Task<StartDownloadResult> StartAsync(StartDownloadRequest request, IProgress<Domain.ValueObjects.DownloadProgress> progress, CancellationToken ct);
    Task<StartDownloadResult> ResumeAsync(Guid jobId, CancellationToken ct);
    Task<StartDownloadResult> RetryAsync(Guid jobId, CancellationToken ct);
}
