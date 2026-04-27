using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Web.Services;

public sealed class JsonCredentialStore : JsonFileRepository, ICredentialStore
{
    public JsonCredentialStore() : base(JsonStorePaths.Credentials) { }

    public async Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct)
    {
        var accounts = await ReadAsync(new List<ProviderAccount>(), ct);
        return accounts.FirstOrDefault(account => account.ProviderId == providerId && account.Label == label);
    }

    public async Task SaveAsync(ProviderAccount account, CancellationToken ct)
    {
        var accounts = await ReadAsync(new List<ProviderAccount>(), ct);
        accounts.RemoveAll(existing => existing.Id == account.Id);
        accounts.Add(account);
        await WriteAsync(accounts, ct);
    }
}
