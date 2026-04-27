using NugsDownloader.Domain.Providers;

namespace NugsDownloader.App.Abstractions;

public sealed class InMemoryProviderCatalog : IProviderCatalog
{
    private readonly IReadOnlyList<IMediaProvider> _providers;

    public InMemoryProviderCatalog(IEnumerable<IMediaProvider> providers)
    {
        _providers = providers.ToArray();
    }

    public IReadOnlyList<IMediaProvider> GetProviders() => _providers;

    public IMediaProvider? FindByUrl(Uri uri) => _providers.FirstOrDefault(provider => provider.CanHandle(uri));

    public IMediaProvider? FindById(string providerId) => _providers.FirstOrDefault(provider => provider.Id == providerId);
}
