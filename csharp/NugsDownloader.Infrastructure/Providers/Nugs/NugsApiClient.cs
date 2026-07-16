using System.Net;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Infrastructure.Providers.Nugs;

public sealed class NugsApiClient
{
    private const string DefaultAuthUrl = "https://id.nugs.net/connect/token";
    private const string DefaultUserInfoUrl = "https://id.nugs.net/connect/userinfo";
    private const string DefaultStashInfoUrl = "https://stash.nugs.net/api/v1/stash";
    private const string DefaultSubInfoUrl = "https://subscriptions.nugs.net/api/v1/me/subscriptions";
    private const string DefaultStreamApiBase = "https://streamapi.nugs.net/";
    private const string ClientId = "Eg7HuH873H65r5rt325UytR5429";
    private const string DevKey = "x7f54tgbdyc64y656thy47er4";
    private const string UserAgent = "NugsNet/3.26.724 (Android; 7.1.2; Asus; ASUS_Z01QD; Scale/2.0; en)";
    private const string UserAgentTwo = "nugsnetAndroid";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly string[] StreamUrlFieldNames = ["streamLink", "streamUrl", "streamURL", "url", "mediaUrl", "downloadUrl"];
    private static readonly string[] StreamUrlContainerFieldNames = ["data", "response", "result"];
    // The current web player negotiates its stream with platform -1 and HLS
    // enabled. Retain known legacy lossless platform fallbacks for accounts or
    // catalog entries that do not answer the web-player request.
    private static readonly int[] AudioPlatformProbeOrder = [-1, 8, 5, 2, 9];

    private readonly HttpClient _http;

    public NugsApiClient()
        : this(CreateClient())
    {
    }

    public NugsApiClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<string?> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "password",
            ["scope"] = "openid profile email nugsnet:api nugsnet:legacyapi offline_access",
            ["username"] = username,
            ["password"] = password
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, DefaultAuthUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var auth = await JsonSerializer.DeserializeAsync<AuthResponse>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
        return auth?.AccessToken;
    }

    public async Task<UserInfo?> GetUserInfoAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DefaultUserInfoUrl);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<UserInfo>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
    }

    public async Task<SubInfo?> GetSubInfoAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DefaultSubInfoUrl);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<SubInfo>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
    }

    public async Task<StashInfo?> GetStashInfoAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DefaultStashInfoUrl);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<StashInfo>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
    }

    public async Task<AlbumMeta?> GetAlbumMetaAsync(string albumId, CancellationToken ct)
    {
        var uri = new UriBuilder(DefaultStreamApiBase + "api.aspx")
        {
            Query = $"method=catalog.container&containerID={Uri.EscapeDataString(albumId)}&vdisp=1"
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<AlbumMeta>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
    }

    public async Task<IReadOnlyList<ArtistMeta>> GetArtistMetaAsync(string artistId, CancellationToken ct)
    {
        var results = new List<ArtistMeta>();
        var offset = 1;

        while (true)
        {
            var uri = new UriBuilder(DefaultStreamApiBase + "api.aspx")
            {
                Query = $"method=catalog.containersAll&limit=100&artistList={Uri.EscapeDataString(artistId)}&availType=1&vdisp=1&startOffset={offset}"
            };

            using var request = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            var meta = await JsonSerializer.DeserializeAsync<ArtistMeta>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
            if (meta?.Response?.Containers is null || meta.Response.Containers.Count == 0)
            {
                break;
            }

            results.Add(meta);
            offset += meta.Response.Containers.Count;
        }

        return results;
    }

    public async Task<PlistMeta?> GetPlaylistMetaAsync(string playlistId, string email, string legacyToken, bool cat, CancellationToken ct)
    {
        var path = cat ? "api.aspx" : "secureApi.aspx";
        var uri = new UriBuilder(DefaultStreamApiBase + path);
        var query = cat
            ? $"method=catalog.playlist&plGUID={Uri.EscapeDataString(playlistId)}"
            : $"method=user.playlist&playlistID={Uri.EscapeDataString(playlistId)}&developerKey={DevKey}&user={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(legacyToken)}";
        uri.Query = query;

        using var request = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgentTwo);
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync<PlistMeta>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
    }

    public async Task DownloadFileAsync(Uri mediaUri, Stream destination, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, mediaUri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await source.CopyToAsync(destination, ct);
    }

    public async Task<Uri> ResolveFullQualityAudioUrlAsync(
        string accessToken,
        string userId,
        string trackId,
        string? preferredFormat,
        CancellationToken ct)
    {
        // Audio subPlayer requests select an entitled stream by platform. The
        // preferred format is not mapped here because the documented audio
        // endpoint contract provides no grounded format-to-platform mapping.
        _ = preferredFormat;

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(trackId))
        {
            throw new InvalidOperationException("Authenticated Nugs subscription details are required to resolve entitled audio.");
        }

        try
        {
            var subscription = await GetSubInfoAsync(accessToken, ct);
            if (subscription is null || !subscription.IsContentAccessible)
            {
                throw new InvalidOperationException("Nugs subscription does not permit entitled audio streaming.");
            }

            var planId = FirstNonEmpty(subscription.Plan?.PlanID, subscription.Promo?.Plan?.PlanID);
            if (string.IsNullOrWhiteSpace(subscription.LegacySubscriptionID) || string.IsNullOrWhiteSpace(planId))
            {
                throw new InvalidOperationException("Nugs subscription details are incomplete for entitled audio streaming.");
            }

            var responseShapes = new List<string>();
            foreach (var platformId in AudioPlatformProbeOrder)
            {
                var uri = new UriBuilder(DefaultStreamApiBase + "bigriver/subPlayer.aspx")
                {
                    Query = string.Join("&", new[]
                    {
                        "platformID=" + platformId.ToString(CultureInfo.InvariantCulture),
                        "trackId=" + Uri.EscapeDataString(trackId),
                        "app=1",
                        "HLS=1",
                        "orgn=websdk",
                        "method=subPlayer",
                        "subscriptionID=" + Uri.EscapeDataString(subscription.LegacySubscriptionID),
                        "subCostplanIDAccessList=" + Uri.EscapeDataString(planId),
                        "nn_userID=" + Uri.EscapeDataString(userId),
                        "startDateStamp=" + Uri.EscapeDataString(ToUnixTimestamp(subscription.StartedAt)),
                        "endDateStamp=" + Uri.EscapeDataString(ToUnixTimestamp(subscription.EndsAt))
                    })
                };

                using var request = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
                request.Headers.TryAddWithoutValidation("User-Agent", UserAgentTwo);
                using var response = await _http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                using var streamDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (TryGetStreamUri(streamDocument.RootElement, out var streamUri))
                {
                    return streamUri;
                }

                responseShapes.Add($"platformID {platformId.ToString(CultureInfo.InvariantCulture)}: {DescribeJsonShape(streamDocument.RootElement)}");
            }

            var diagnostic = responseShapes.Count > 0
                ? $" Response shapes: {string.Join("; ", responseShapes)}."
                : string.Empty;
            throw new InvalidOperationException("Nugs did not return an entitled audio URL." + diagnostic);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException)
        {
            throw new InvalidOperationException("Nugs entitled audio resolution failed.", ex);
        }
    }

    private static string ToUnixTimestamp(string value)
    {
        const DateTimeStyles utcStyles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (!DateTimeOffset.TryParseExact(value, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture, utcStyles, out var timestamp)
            && !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, utcStyles, out timestamp))
        {
            throw new FormatException("Nugs subscription timestamp was invalid.");
        }

        return timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool TryGetStreamUri(JsonElement root, out Uri streamUri)
    {
        if (TryGetStreamUriFromObject(root, out streamUri))
        {
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (MatchesAllowedFieldName(property.Name, StreamUrlContainerFieldNames)
                    && TryGetStreamUriFromObject(property.Value, out streamUri))
                {
                    return true;
                }
            }
        }

        streamUri = null!;
        return false;
    }

    private static bool TryGetStreamUriFromObject(JsonElement element, out Uri streamUri)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (MatchesAllowedFieldName(property.Name, StreamUrlFieldNames)
                    && property.Value.ValueKind == JsonValueKind.String
                    && Uri.TryCreate(property.Value.GetString(), UriKind.Absolute, out var candidate)
                    && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
                {
                    streamUri = candidate;
                    return true;
                }
            }
        }

        streamUri = null!;
        return false;
    }

    private static bool MatchesAllowedFieldName(string value, IEnumerable<string> allowedNames) =>
        allowedNames.Any(allowedName => string.Equals(value, allowedName, StringComparison.OrdinalIgnoreCase));

    private static string DescribeJsonShape(JsonElement element)
    {
        var kind = DescribeJsonValueKind(element.ValueKind);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return kind;
        }

        return $"{kind}{{{string.Join(", ", element.EnumerateObject().Select(property => $"{property.Name}:{DescribeJsonValueKind(property.Value.ValueKind)}"))}}}";
    }

    private static string DescribeJsonValueKind(JsonValueKind valueKind) => valueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "bool",
        JsonValueKind.Null => "null",
        _ => "unknown"
    };

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        return new HttpClient(handler, disposeHandler: false);
    }

    public sealed record AuthResponse([property: JsonPropertyName("access_token")] string AccessToken);
    public sealed record UserInfo(string Sub);
    public sealed record StashInfo(string UserId);
    public sealed record SubInfo(Plan Plan, Promo Promo, string LegacySubscriptionID, string StartedAt, string EndsAt, bool IsContentAccessible, List<ProductFormatList> ProductFormatList);
    public sealed record Plan(string Description, string PlanID);
    public sealed record Promo(Plan Plan);
    public sealed record ProductFormatList(string FormatStr, int SkuID);
    public sealed record AlbumMeta(AlbumMetaBody? Response);
    public sealed record AlbumMetaBody(string ArtistName, string ContainerInfo, string ContainerTypeStr, List<Track> Tracks);
    public sealed record ArtistMeta(ArtistMetaBody? Response);
    public sealed record ArtistMetaBody(List<AlbumBody> Containers);
    public sealed record AlbumBody(string ArtistName, List<Track> Tracks);
    public sealed record PlistMeta(PlistBody? Response);
    public sealed record PlistBody(string PlayListName, List<PlistItemBody> Items);
    public sealed record PlistItemBody(Track Track);
    public sealed record Track(int TrackID, string SongTitle, string? ClipURL = null);
}
