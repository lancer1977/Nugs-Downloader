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

    [Fact]
    public async Task ResolveFullQualityAudioUrlAsync_UsesDocumentedAudioRequestParameters()
    {
        Uri? requestUri = null;
        using var handler = new FakeHttpHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("me/subscriptions", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new
                {
                    plan = new { description = "Subscriber", planID = "plan-123" },
                    promo = new { plan = new { description = "", planID = "" } },
                    legacySubscriptionID = "subscription-456",
                    startedAt = "01/02/2026 03:04:05",
                    endsAt = "12/31/2026 23:59:59",
                    isContentAccessible = true,
                    productFormatList = Array.Empty<object>()
                }));
            }

            if (url.Contains("bigriver/subPlayer.aspx", StringComparison.Ordinal))
            {
                requestUri = request.RequestUri;
                return Task.FromResult(JsonResponse(new { streamLink = "https://media.example.test/full-quality.flac?sig=test-signature" }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var client = new NugsApiClient(new HttpClient(handler));
        var result = await client.ResolveFullQualityAudioUrlAsync("test-access-token", "user-789", "12345", "flac", CancellationToken.None);

        Assert.Equal("https://media.example.test/full-quality.flac?sig=test-signature", result.AbsoluteUri);
        Assert.NotNull(requestUri);
        Assert.Equal("1", QueryValue(requestUri!, "platformID"));
        Assert.Equal("12345", QueryValue(requestUri!, "trackID"));
        Assert.Equal("subscription-456", QueryValue(requestUri!, "subscriptionID"));
        Assert.Equal("plan-123", QueryValue(requestUri!, "subCostplanIDAccessList"));
        Assert.Equal("user-789", QueryValue(requestUri!, "nn_userID"));
        Assert.Equal("1", QueryValue(requestUri!, "app"));
        Assert.Null(QueryValue(requestUri!, "skuId"));
        Assert.Null(QueryValue(requestUri!, "containerID"));
        Assert.Null(QueryValue(requestUri!, "chap"));
    }

    [Fact]
    public async Task ResolveFullQualityAudioUrlAsync_ProbesNextPlatformAfterNullStreamLink()
    {
        var requestUris = new List<Uri>();
        using var handler = new FakeHttpHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("me/subscriptions", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new
                {
                    plan = new { description = "Subscriber", planID = "plan-123" },
                    promo = new { plan = new { description = "", planID = "" } },
                    legacySubscriptionID = "subscription-456",
                    startedAt = "01/02/2026 03:04:05",
                    endsAt = "12/31/2026 23:59:59",
                    isContentAccessible = true,
                    productFormatList = Array.Empty<object>()
                }));
            }

            if (url.Contains("bigriver/subPlayer.aspx", StringComparison.Ordinal))
            {
                requestUris.Add(request.RequestUri!);
                var platformId = QueryValue(request.RequestUri!, "platformID");
                string? streamLink = platformId == "1" ? null : "https://media.example.test/full-quality.flac";
                return Task.FromResult(JsonResponse(new { streamLink }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var client = new NugsApiClient(new HttpClient(handler));
        var result = await client.ResolveFullQualityAudioUrlAsync("test-access-token", "user-789", "12345", "flac", CancellationToken.None);

        Assert.Equal("https://media.example.test/full-quality.flac", result.AbsoluteUri);
        Assert.Collection(
            requestUris,
            first =>
            {
                Assert.Equal("1", QueryValue(first, "platformID"));
                Assert.Equal("12345", QueryValue(first, "trackID"));
            },
            second =>
            {
                Assert.Equal("4", QueryValue(second, "platformID"));
                Assert.Equal("12345", QueryValue(second, "trackID"));
            });
    }

    [Fact]
    public async Task ResolveFullQualityAudioUrlAsync_AcceptsIso8601SubscriptionTimestamps()
    {
        using var handler = new FakeHttpHandler(request =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (url.Contains("me/subscriptions", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new
                {
                    plan = new { description = "Subscriber", planID = "plan-123" },
                    promo = new { plan = new { description = "", planID = "" } },
                    legacySubscriptionID = "subscription-456",
                    startedAt = "2026-01-02T03:04:05Z",
                    endsAt = "2026-12-31T23:59:59Z",
                    isContentAccessible = true,
                    productFormatList = Array.Empty<object>()
                }));
            }

            if (url.Contains("bigriver/subPlayer.aspx", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(new { streamLink = "https://media.example.test/full-quality.flac" }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var client = new NugsApiClient(new HttpClient(handler));

        var result = await client.ResolveFullQualityAudioUrlAsync("test-access-token", "user-789", "12345", "flac", CancellationToken.None);

        Assert.Equal("https://media.example.test/full-quality.flac", result.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveFullQualityAudioUrlAsync_AcceptsStreamUrlWithUppercaseUrlSuffix()
    {
        using var handler = CreateEntitledAudioHandler(new { streamURL = "https://media.example.test/full-quality.flac" });
        var client = new NugsApiClient(new HttpClient(handler));

        var result = await client.ResolveFullQualityAudioUrlAsync("test-access-token", "user-789", "12345", "flac", CancellationToken.None);

        Assert.Equal("https://media.example.test/full-quality.flac", result.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveFullQualityAudioUrlAsync_AcceptsStreamUrlWithinDataObject()
    {
        using var handler = CreateEntitledAudioHandler(new { data = new { streamUrl = "https://media.example.test/full-quality.flac" } });
        var client = new NugsApiClient(new HttpClient(handler));

        var result = await client.ResolveFullQualityAudioUrlAsync("test-access-token", "user-789", "12345", "flac", CancellationToken.None);

        Assert.Equal("https://media.example.test/full-quality.flac", result.AbsoluteUri);
    }

    [Fact]
    public async Task ResolveFullQualityAudioUrlAsync_WhenNoUrlReturned_ReportsOnlyResponseShape()
    {
        const string tokenLikeValue = "eyJhbGciOiJIUzI1NiJ9.secret-signature";
        using var handler = CreateEntitledAudioHandler(new
        {
            streamLink = (string?)null,
            token = tokenLikeValue,
            nested = new { signedUrl = "https://media.example.test/secret" },
            attempts = 3,
            entitled = true,
            missing = (string?)null,
            items = new[] { "not-a-url" }
        });
        var client = new NugsApiClient(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ResolveFullQualityAudioUrlAsync("test-access-token", "user-789", "12345", "flac", CancellationToken.None));

        Assert.DoesNotContain(tokenLikeValue, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("https://media.example.test/secret", exception.Message, StringComparison.Ordinal);
        const string expectedResponseShapes = "Response shapes: "
            + "platformID 1: object{streamLink:null, token:string, nested:object, attempts:number, entitled:bool, missing:null, items:array}; "
            + "platformID 4: object{streamLink:null, token:string, nested:object, attempts:number, entitled:bool, missing:null, items:array}; "
            + "platformID 7: object{streamLink:null, token:string, nested:object, attempts:number, entitled:bool, missing:null, items:array}; "
            + "platformID 10: object{streamLink:null, token:string, nested:object, attempts:number, entitled:bool, missing:null, items:array}.";
        Assert.Contains(expectedResponseShapes, exception.Message, StringComparison.Ordinal);
    }

    private static FakeHttpHandler CreateEntitledAudioHandler(object playerResponse) => new(request =>
    {
        var url = request.RequestUri?.AbsoluteUri ?? string.Empty;
        if (url.Contains("me/subscriptions", StringComparison.Ordinal))
        {
            return Task.FromResult(JsonResponse(new
            {
                plan = new { description = "Subscriber", planID = "plan-123" },
                promo = new { plan = new { description = "", planID = "" } },
                legacySubscriptionID = "subscription-456",
                startedAt = "01/02/2026 03:04:05",
                endsAt = "12/31/2026 23:59:59",
                isContentAccessible = true,
                productFormatList = Array.Empty<object>()
            }));
        }

        return Task.FromResult(url.Contains("bigriver/subPlayer.aspx", StringComparison.Ordinal)
            ? JsonResponse(playerResponse)
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    });

    private static HttpResponseMessage JsonResponse(object value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value)
    };

    private static string? QueryValue(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        return query
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.Ordinal))
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .SingleOrDefault();
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
