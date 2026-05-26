using NugsDownloader.Domain.ValueObjects;
using Xunit;

namespace NugsDownloader.Tests;

public class CredentialsTests
{
    [Fact]
    public void Credentials_StoresValues()
    {
        var credentials = new Credentials("alice", "secret", "token", "label");

        Assert.Equal("alice", credentials.Username);
        Assert.Equal("secret", credentials.Password);
        Assert.Equal("token", credentials.Token);
        Assert.Equal("label", credentials.Label);
    }

    [Fact]
    public void Credentials_AllowsOptionalValuesByDefault()
    {
        var credentials = new Credentials("alice", null, null);

        Assert.Null(credentials.Password);
        Assert.Null(credentials.Token);
        Assert.Null(credentials.Label);
    }

    [Fact]
    public void Credentials_WithReturnsUpdatedCopy()
    {
        var baseCredentials = new Credentials("alice", "secret", null);
        var updated = baseCredentials with { Label = "primary" };

        Assert.Equal("primary", updated.Label);
        Assert.Equal(baseCredentials.Username, updated.Username);
    }
}
