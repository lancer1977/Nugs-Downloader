namespace NugsDownloader.App.Abstractions;

public interface ISecretVault
{
    Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct);
    Task<string?> GetAsync(string secretRef, CancellationToken ct);
    Task DeleteAsync(string secretRef, CancellationToken ct);
}

