using NugsDownloader.Domain.ValueObjects;

using Xunit;

namespace NugsDownloader.Tests;

public class AuthResultRecordTests
{
    [Fact]
    public void AuthResult_StoresSuccessPathFields()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(4);
        var auth = new AuthResult(
            true,
            "nugs",
            "secret-ref",
            "Nugs",
            expiresAt,
            "ok");

        Assert.True(auth.Success);
        Assert.Equal("nugs", auth.ProviderId);
        Assert.Equal("Nugs", auth.DisplayName);
        Assert.Equal(expiresAt, auth.ExpiresAt);
        Assert.Equal("secret-ref", auth.SecretRef);
    }

    [Fact]
    public void AuthResult_WithUpdatesMessage()
    {
        var auth = new AuthResult(
            false,
            "nugs",
            null,
            null,
            null,
            "invalid token");

        var retried = auth with { Message = "fresh token", Success = true };

        Assert.False(auth.Success);
        Assert.Equal("invalid token", auth.Message);
        Assert.True(retried.Success);
        Assert.Equal("fresh token", retried.Message);
    }
}
