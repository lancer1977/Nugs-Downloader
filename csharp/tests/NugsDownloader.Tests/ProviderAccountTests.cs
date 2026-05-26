using NugsDownloader.Domain.Entities;
using Xunit;

namespace NugsDownloader.Tests;

public class ProviderAccountTests
{
    [Fact]
    public void ProviderAccount_StoresValues()
    {
        var account = new ProviderAccount(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "nugs",
            "primary",
            "alice",
            "nugs:primary",
            AuthenticationState.Valid,
            DateTimeOffset.UtcNow);

        Assert.Equal("nugs", account.ProviderId);
        Assert.Equal(AuthenticationState.Valid, account.AuthState);
        Assert.Equal("alice", account.Username);
    }

    [Fact]
    public void ProviderAccount_WithReturnsUpdatedState()
    {
        var account = new ProviderAccount(
            Guid.NewGuid(),
            "nugs",
            "primary",
            "alice",
            "nugs:primary",
            AuthenticationState.Unknown,
            null);

        var updated = account with { AuthState = AuthenticationState.Invalid };

        Assert.Equal(AuthenticationState.Invalid, updated.AuthState);
        Assert.Equal(account.ProviderId, updated.ProviderId);
    }
}
