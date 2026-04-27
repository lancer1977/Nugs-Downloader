using NugsDownloader.Domain.Providers;

namespace NugsDownloader.App.Abstractions;

public interface IProviderCatalog
{
    IReadOnlyList<IMediaProvider> GetProviders();
    IMediaProvider? FindByUrl(Uri uri);
    IMediaProvider? FindById(string providerId);
}

