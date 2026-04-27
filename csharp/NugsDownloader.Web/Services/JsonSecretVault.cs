using System.Text.Json;
using NugsDownloader.App.Abstractions;

namespace NugsDownloader.Web.Services;

public sealed class JsonSecretVault : JsonFileRepository, ISecretVault
{
    public JsonSecretVault() : base(JsonStorePaths.Secrets) { }

    public async Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct)
    {
        var secrets = await ReadAsync(new Dictionary<string, string>(), ct);
        var secretRef = $"{providerId}:{label}";
        secrets[secretRef] = secret;
        await WriteAsync(secrets, ct);
        return secretRef;
    }

    public async Task<string?> GetAsync(string secretRef, CancellationToken ct)
    {
        var secrets = await ReadAsync(new Dictionary<string, string>(), ct);
        return secrets.TryGetValue(secretRef, out var secret) ? secret : null;
    }

    public async Task DeleteAsync(string secretRef, CancellationToken ct)
    {
        var secrets = await ReadAsync(new Dictionary<string, string>(), ct);
        if (secrets.Remove(secretRef))
        {
            await WriteAsync(secrets, ct);
        }
    }
}
