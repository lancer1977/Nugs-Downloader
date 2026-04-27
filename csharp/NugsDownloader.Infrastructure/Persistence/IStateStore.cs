using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Infrastructure.Persistence;

public interface IStateStore
{
    Task InitializeAsync(CancellationToken ct);
    Task<IReadOnlyList<DownloadJob>> ListJobsAsync(CancellationToken ct);
}

