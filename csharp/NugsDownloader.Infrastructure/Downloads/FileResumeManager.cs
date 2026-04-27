using System.Security.Cryptography;
using System.Text.Json;

namespace NugsDownloader.Infrastructure.Downloads;

public sealed class FileResumeManager : IResumeManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _stateDir;

    public FileResumeManager(string stateDir)
    {
        _stateDir = stateDir;
    }

    public string GetStatePath(string filePath)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(filePath));
        return Path.Combine(_stateDir, Convert.ToHexString(hash).ToLowerInvariant() + ".resume.json");
    }

    public async Task SaveStateAsync(ResumeState state, CancellationToken ct)
    {
        Directory.CreateDirectory(_stateDir);

        var updated = state with { UpdatedAt = DateTimeOffset.UtcNow };
        var json = JsonSerializer.Serialize(updated, JsonOptions);
        var statePath = GetStatePath(updated.FilePath);
        var tempPath = statePath + ".tmp";

        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, statePath, overwrite: true);
    }

    public async Task<ResumeState?> LoadStateAsync(string filePath, CancellationToken ct)
    {
        var statePath = GetStatePath(filePath);
        if (!File.Exists(statePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(statePath, ct);
        return JsonSerializer.Deserialize<ResumeState>(json, JsonOptions);
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

    public static string CalculateChecksumFromBytes(byte[] data)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(data)).ToLowerInvariant();
    }

    public static string CalculateChecksum(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
    }

    public static void ValidatePartialDownload(ResumeState state)
    {
        var fileInfo = new FileInfo(state.FilePath);
        if (!fileInfo.Exists)
        {
            throw new InvalidOperationException("partial file no longer exists");
        }

        if (fileInfo.Length != state.DownloadedSize)
        {
            throw new InvalidOperationException($"file size mismatch: expected {state.DownloadedSize}, got {fileInfo.Length}");
        }

        if (DateTimeOffset.UtcNow - state.UpdatedAt > TimeSpan.FromHours(24))
        {
            throw new InvalidOperationException("resume state is too old");
        }
    }

    public static ResumeState CreateInitialState(string filePath, string url, long totalSize, string etag) =>
        new(filePath, url, totalSize, 0, DateTimeOffset.UtcNow, etag, string.Empty, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
