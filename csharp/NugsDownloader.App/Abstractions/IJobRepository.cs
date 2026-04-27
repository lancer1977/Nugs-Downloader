using NugsDownloader.Domain.Entities;

namespace NugsDownloader.App.Abstractions;

public interface IJobRepository
{
    Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct);
    Task SaveAsync(DownloadJob job, CancellationToken ct);
}

