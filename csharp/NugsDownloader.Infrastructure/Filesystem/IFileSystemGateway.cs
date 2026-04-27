namespace NugsDownloader.Infrastructure.Filesystem;

public interface IFileSystemGateway
{
    Task EnsureDirectoryAsync(string path, CancellationToken ct);
    Task<bool> ExistsAsync(string path, CancellationToken ct);
}

