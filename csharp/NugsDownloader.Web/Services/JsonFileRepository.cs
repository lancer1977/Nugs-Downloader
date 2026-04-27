using System.Text.Json;

namespace NugsDownloader.Web.Services;

public abstract class JsonFileRepository
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    protected readonly string Path;

    protected JsonFileRepository(string path)
    {
        Path = path;
    }

    protected async Task<T> ReadAsync<T>(T fallback, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(Path))
            {
                return fallback;
            }

            var json = await File.ReadAllTextAsync(Path, ct);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        finally
        {
            _gate.Release();
        }
    }

    protected async Task WriteAsync<T>(T value, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(value, JsonOptions);
            await File.WriteAllTextAsync(Path, json, ct);
        }
        finally
        {
            _gate.Release();
        }
    }
}
