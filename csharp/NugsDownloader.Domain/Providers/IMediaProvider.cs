using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Domain.Providers;

public interface IMediaProvider
{
    string Id { get; }
    string DisplayName { get; }
    ProviderCapabilities Capabilities { get; }

    bool CanHandle(Uri uri);
    Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct);
    Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct);
    Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct);
    Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct);
}

