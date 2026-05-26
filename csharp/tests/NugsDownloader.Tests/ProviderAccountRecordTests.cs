using NugsDownloader.Domain.Entities;

using Xunit;

namespace NugsDownloader.Tests;

public class ProviderAccountRecordTests
{
    [Fact]
    public void ProviderAccount_StoresCredentialState()
    {
        var now = DateTimeOffset.UtcNow;
        var account = new ProviderAccount(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "nugs",
            "primary",
            "alice",
            "vault-ref",
            AuthenticationState.Valid,
            now);

        Assert.Equal("nugs", account.ProviderId);
        Assert.Equal("alice", account.Username);
        Assert.Equal(AuthenticationState.Valid, account.AuthState);
        Assert.Equal(now, account.LastVerifiedAt);
        Assert.Equal("vault-ref", account.SecretRef);
    }

    [Fact]
    public void ProviderAccount_WithUpdatesAuthState()
    {
        var account = new ProviderAccount(
            Guid.NewGuid(),
            "livephish",
            "default",
            "bob",
            "secret-1",
            AuthenticationState.Invalid,
            null);

        var refreshed = account with { AuthState = AuthenticationState.Valid };

        Assert.Equal(AuthenticationState.Invalid, account.AuthState);
        Assert.Equal(AuthenticationState.Valid, refreshed.AuthState);
        Assert.Equal("default", refreshed.Label);
    }
}
