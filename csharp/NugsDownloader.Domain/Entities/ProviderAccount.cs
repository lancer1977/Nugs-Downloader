namespace NugsDownloader.Domain.Entities;

public sealed record ProviderAccount(
    Guid Id,
    string ProviderId,
    string Label,
    string Username,
    string SecretRef,
    AuthenticationState AuthState,
    DateTimeOffset? LastVerifiedAt);

public enum AuthenticationState
{
    Unknown,
    Valid,
    Expired,
    Invalid
}

