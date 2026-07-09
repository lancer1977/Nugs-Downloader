using NugsDownloader.App.UseCases;

namespace NugsDownloader.Web.Services;

public interface IBackgroundDownloadQueue
{
    Task<StartDownloadResult> EnqueueAsync(StartDownloadRequest request, CancellationToken ct);
    Task<StartDownloadResult> CancelAsync(Guid jobId, CancellationToken ct);
}
