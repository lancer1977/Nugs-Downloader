using NugsDownloader.App.UseCases;

namespace NugsDownloader.Web.Services;

public interface IBackgroundDownloadQueue
{
    Task<StartDownloadResult> EnqueueAsync(StartDownloadRequest request, CancellationToken ct);
    Task<StartDownloadResult> ResumeAsync(Guid jobId, CancellationToken ct);
    Task<StartDownloadResult> RetryAsync(Guid jobId, CancellationToken ct);
    Task<StartDownloadResult> CancelAsync(Guid jobId, CancellationToken ct);
}
