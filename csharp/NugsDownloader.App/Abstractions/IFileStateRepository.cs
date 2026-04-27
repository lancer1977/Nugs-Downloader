using NugsDownloader.Domain.Entities;

namespace NugsDownloader.App.Abstractions;

public interface IFileStateRepository
{
    Task<IReadOnlyList<FileState>> GetByJobAsync(Guid jobId, CancellationToken ct);
    Task SaveAsync(FileState state, CancellationToken ct);
}

