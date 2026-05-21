using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Web.Services;

public sealed class JsonFileStateRepository : JsonFileRepository, IFileStateRepository
{
    public JsonFileStateRepository(JsonStorePaths paths) : base(paths.FileStates) { }

    public async Task<IReadOnlyList<FileState>> GetByJobAsync(Guid jobId, CancellationToken ct)
    {
        var states = await ReadAsync(new List<FileState>(), ct);
        return states.Where(state => state.JobId == jobId).ToList();
    }

    public async Task SaveAsync(FileState state, CancellationToken ct)
    {
        var states = await ReadAsync(new List<FileState>(), ct);
        states.RemoveAll(existing => existing.Id == state.Id);
        states.Add(state);
        await WriteAsync(states, ct);
    }
}
