using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class AuthResultTests
{
    [Fact]
    public void AuthResult_StoresValues()
    {
        var expiresAt = DateTimeOffset.UtcNow;
        var result = new AuthResult(
            Success: true,
            ProviderId: "nugs",
            SecretRef: "secret-ref",
            DisplayName: "Nugs Account",
            ExpiresAt: expiresAt,
            Message: "ok");

        Assert.True(result.Success);
        Assert.Equal("nugs", result.ProviderId);
        Assert.Equal("secret-ref", result.SecretRef);
        Assert.Equal("Nugs Account", result.DisplayName);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.Equal("ok", result.Message);
    }

    [Fact]
    public void AuthResult_WithReturnsUpdatedFailureState()
    {
        var result = new AuthResult(true, "nugs", "secret-ref", "Nugs", DateTimeOffset.UtcNow, null);
        var failed = result with { Success = false, Message = "denied", ExpiresAt = null };

        Assert.False(failed.Success);
        Assert.Equal("denied", failed.Message);
        Assert.Null(failed.ExpiresAt);
    }
}
