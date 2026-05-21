using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using NugsDownloader.App.Abstractions;

namespace NugsDownloader.Web.Services;

public sealed class JsonSecretVault : JsonFileRepository, ISecretVault
{
    private const string ProtectedPrefix = "protected:";
    private readonly IDataProtector _protector;

    public JsonSecretVault(JsonStorePaths paths, IDataProtectionProvider dataProtectionProvider)
        : base(paths.Secrets)
    {
        _protector = dataProtectionProvider.CreateProtector("NugsDownloader.SecretVault.v1");
    }

    public async Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct)
    {
        var secrets = await ReadAsync(new Dictionary<string, string>(), ct);
        var secretRef = $"{providerId}:{label}";
        secrets[secretRef] = ProtectedPrefix + _protector.Protect(secret);
        await WriteAsync(secrets, ct);
        return secretRef;
    }

    public async Task<string?> GetAsync(string secretRef, CancellationToken ct)
    {
        var secrets = await ReadAsync(new Dictionary<string, string>(), ct);
        if (!secrets.TryGetValue(secretRef, out var secret))
        {
            return null;
        }

        if (!secret.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return secret;
        }

        return _protector.Unprotect(secret[ProtectedPrefix.Length..]);
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
