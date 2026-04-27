namespace NugsDownloader.Domain.ValueObjects;

public sealed record AuthResult(
    bool Success,
    string ProviderId,
    string? SecretRef,
    string? DisplayName,
    DateTimeOffset? ExpiresAt,
    string? Message);

