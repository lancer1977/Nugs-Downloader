namespace NugsDownloader.Infrastructure.Downloads;

public interface IResumeManager
{
    Task<ResumeState?> LoadStateAsync(string filePath, CancellationToken ct);
    Task SaveStateAsync(ResumeState state, CancellationToken ct);
    Task DeleteStateAsync(string filePath, CancellationToken ct);
}
