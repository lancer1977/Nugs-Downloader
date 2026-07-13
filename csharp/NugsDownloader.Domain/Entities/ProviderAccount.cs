namespace NugsDownloader.Domain.Entities;

public sealed record ProviderAccount(
    Guid Id,
    string ProviderId,
    string Label,
    string Username,
    string SecretRef,
    AuthenticationState AuthState,
    DateTimeOffset? LastVerifiedAt,
    CredentialKind CredentialKind = CredentialKind.Password);

public enum CredentialKind
{
    Password,
    Token
}

public enum AuthenticationState
{
    Unknown,
    Valid,
    Expired,
    Invalid
}
