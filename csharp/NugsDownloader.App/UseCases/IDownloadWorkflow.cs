namespace NugsDownloader.App.UseCases;

public interface IDownloadWorkflow
{
    Task<StartDownloadResult> StartAsync(StartDownloadRequest request, IProgress<Domain.ValueObjects.DownloadProgress> progress, CancellationToken ct);
}

