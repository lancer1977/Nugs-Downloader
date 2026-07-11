using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Infrastructure.Providers.Nugs;

public sealed class NugsApiClient
{
    private const string DefaultAuthUrl = "https://id.nugs.net/connect/token";
    private const string DefaultUserInfoUrl = "https://id.nugs.net/connect/userinfo";
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
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Nugs media download request failed.");
        }

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

            var uri = new UriBuilder(DefaultStreamApiBase + "bigriver/subPlayer.aspx")
            {
                Query = string.Join("&", new[]
                {
                    "platformID=" + Uri.EscapeDataString(ResolvePlatformId(preferredFormat).ToString(CultureInfo.InvariantCulture)),
                    "trackID=" + Uri.EscapeDataString(trackId),
                    "app=1",
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
                throw new InvalidOperationException("Nugs entitled audio resolution request failed.");
            }

            var stream = await JsonSerializer.DeserializeAsync<StreamMeta>(await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);
            if (string.IsNullOrWhiteSpace(stream?.StreamLink) || !Uri.TryCreate(stream.StreamLink, UriKind.Absolute, out var streamUri))
            {
                throw new InvalidOperationException("Nugs did not return an entitled audio URL.");
            }

            return streamUri;
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

    private static int ResolvePlatformId(string? preferredFormat) => preferredFormat?.Trim().ToLowerInvariant() switch
    {
        "alac" => 1,
        "flac" => 2,
        "mqa" => 3,
        "aac" => 5,
        _ => 2
    };

    private static string ToUnixTimestamp(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var timestamp))
        {
            throw new FormatException("Nugs subscription timestamp was invalid.");
        }

        return timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

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
    public sealed record StreamMeta([property: JsonPropertyName("streamLink")] string? StreamLink);
}
