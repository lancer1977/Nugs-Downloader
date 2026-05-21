using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Web.Services;

public sealed class JsonJobRepository : JsonFileRepository, IJobRepository
{
    public JsonJobRepository(JsonStorePaths paths) : base(paths.Jobs) { }

    public async Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct)
    {
        var jobs = await ReadAsync(new List<DownloadJob>(), ct);
        return jobs.FirstOrDefault(job => job.Id == id);
    }

    public async Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) =>
        await ReadAsync(new List<DownloadJob>(), ct);

    public async Task SaveAsync(DownloadJob job, CancellationToken ct)
    {
        var jobs = await ReadAsync(new List<DownloadJob>(), ct);
        jobs.RemoveAll(existing => existing.Id == job.Id);
        jobs.Add(job);
        await WriteAsync(jobs, ct);
    }
}
