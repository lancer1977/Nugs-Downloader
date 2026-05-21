using System.Security.Cryptography;
using System.Text.Json;

namespace NugsDownloader.Infrastructure.Downloads;

public sealed class FileResumeManager
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _stateDirectory;

    public FileResumeManager(string stateDirectory)
    {
        _stateDirectory = stateDirectory;
    }

    public static ResumeState CreateInitialState(string filePath, string url, long totalSize, string? etag)
    {
        var now = DateTimeOffset.UtcNow;
        return new ResumeState(filePath, url, totalSize, 0, etag, null, now, now);
    }

    public async Task SaveStateAsync(ResumeState state, CancellationToken ct)
    {
        Directory.CreateDirectory(_stateDirectory);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(GetStatePath(state.FilePath), json, ct);
    }

    public async Task<ResumeState?> LoadStateAsync(string filePath, CancellationToken ct)
    {
        var statePath = GetStatePath(filePath);
        if (!File.Exists(statePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<ResumeState>(stream, JsonOptions, ct);
    }

    public Task DeleteStateAsync(string filePath, CancellationToken ct)
    {
        var statePath = GetStatePath(filePath);
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        return Task.CompletedTask;
    }

    public static void ValidatePartialDownload(ResumeState state)
    {
        if (!File.Exists(state.FilePath))
        {
            throw new InvalidOperationException($"The partial file no longer exists: {state.FilePath}");
        }

        var actualSize = new FileInfo(state.FilePath).Length;
        if (actualSize != state.DownloadedSize)
        {
            throw new InvalidOperationException($"The partial file size mismatch for {state.FilePath}: expected {state.DownloadedSize}, found {actualSize}.");
        }

        if (state.UpdatedAt < DateTimeOffset.UtcNow.AddHours(-24))
        {
            throw new InvalidOperationException($"The resume state is too old for {state.FilePath}.");
        }
    }

    public static string CalculateChecksumFromBytes(byte[] bytes)
    {
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string GetStatePath(string filePath)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(filePath));
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return Path.Combine(_stateDirectory, $"{hash}.resume.json");
    }
}
