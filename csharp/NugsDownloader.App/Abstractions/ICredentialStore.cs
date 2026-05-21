using NugsDownloader.Domain.Entities;

namespace NugsDownloader.App.Abstractions;

public interface ICredentialStore
{
    Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct);
    Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct);
    Task SaveAsync(ProviderAccount account, CancellationToken ct);
}
