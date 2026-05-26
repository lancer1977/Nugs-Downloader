using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NugsDownloader.Infrastructure.Providers.LivePhish;

public sealed class LivePhishApiClient
{
    private const string DefaultAuthUrl = "https://id.livephish.com/connect/token";
    private const string DefaultUserInfoUrl = "https://id.livephish.com/connect/userinfo";
    private const string DefaultStreamApiBase = "https://www.livephish.com/";
    private const string ClientId = "Fujeij8d764ydxcnh4676scsr7f4";
    private const string Scope = "offline_access nugsnet:api nugsnet:legacyapi";
    private const string UserAgent = "LivePhish/3.4.5.357 (Android; 7.1.2; Asus; ASUS_Z01QD)";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public LivePhishApiClient()
        : this(CreateClient())
    {
    }

    public LivePhishApiClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task<string?> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["grant_type"] = "password",
            ["scope"] = Scope,
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
    public sealed record AlbumMeta(AlbumMetaBody? Response);
    public sealed record AlbumMetaBody(string ArtistName, string ContainerInfo, string? ContainerTypeStr, List<Track> Tracks);
    public sealed record Track(int TrackID, string SongTitle);
}