using System.Net;
using System.Net.Http.Json;
using NugsDownloader.Infrastructure.Providers.Nugs;
using Xunit;

namespace NugsDownloader.Tests;

public class NugsApiClientTests
{
    [Fact]
    public async Task AuthenticateAsync_ReturnsTokenFromAuthEndpoint()
    {
        using var handler = new FakeHttpHandler(async request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("connect/token") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { access_token = "token-123" })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new NugsApiClient(new HttpClient(handler));
        var token = await client.AuthenticateAsync("user", "pass", CancellationToken.None);

        Assert.Equal("token-123", token);
    }

    [Fact]
    public async Task GetUserInfoAsync_ReturnsUserInfoFromEndpoint()
    {
        using var handler = new FakeHttpHandler(async request =>
        {
            if (request.RequestUri?.AbsoluteUri.Contains("connect/userinfo") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { sub = "user-123" })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new NugsApiClient(new HttpClient(handler));
        var user = await client.GetUserInfoAsync("token-123", CancellationToken.None);

        Assert.Equal("user-123", user?.Sub);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request);
    }
}
