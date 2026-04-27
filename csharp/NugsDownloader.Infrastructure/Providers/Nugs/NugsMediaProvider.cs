using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Infrastructure.Providers.Nugs;

public sealed class NugsMediaProvider : INugsProvider
{
    private static readonly HashSet<string> AlbumSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "release",
        "album"
    };

    private static readonly HashSet<string> ArtistSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "artist",
        "browse"
    };

    private readonly NugsApiClient _apiClient;

    public NugsMediaProvider()
        : this(new NugsApiClient())
    {
    }

    public NugsMediaProvider(NugsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public string Id => "nugs";

    public string DisplayName => "Nugs";

    public ProviderCapabilities Capabilities { get; } = new(
        SupportsAudio: true,
        SupportsVideo: true,
        SupportsChapters: true,
        SupportsResume: true,
        SupportsTokens: true,
        SupportsPasswordLogin: true,
        SupportedFormats: new[] { "alac", "flac", "mqa", "aac" },
        SupportedResolutions: new[] { "480p", "720p", "1080p", "1440p", "4k" });

    public bool CanHandle(Uri uri) =>
        uri.Host.Contains("nugs.net", StringComparison.OrdinalIgnoreCase)
        || uri.Host.Contains("2nu.gs", StringComparison.OrdinalIgnoreCase);

    public async Task<AuthResult> AuthenticateAsync(Credentials credentials, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credentials.Username) || string.IsNullOrWhiteSpace(credentials.Password))
        {
            return new AuthResult(false, Id, null, credentials.Label ?? credentials.Username, null, "Missing username or password.");
        }

        var token = await _apiClient.AuthenticateAsync(credentials.Username, credentials.Password, ct);
        if (token is null)
        {
            return new AuthResult(false, Id, null, credentials.Label ?? credentials.Username, null, "Authentication failed.");
        }

        var userInfo = await _apiClient.GetUserInfoAsync(token, ct);
        return new AuthResult(
            Success: true,
            ProviderId: Id,
            SecretRef: token,
            DisplayName: credentials.Label ?? credentials.Username,
            ExpiresAt: null,
            Message: userInfo is null ? "Authenticated" : $"Authenticated as {userInfo.Sub}");
    }

    public async Task<MediaDiscoveryResult> DiscoverAsync(Uri uri, CancellationToken ct)
    {
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var kind = ResolveKind(segments);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = uri.Host,
            ["kind"] = kind,
            ["path"] = uri.AbsolutePath
        };

        string title = kind;
        string? artist = null;
        IReadOnlyList<MediaItem> items = Array.Empty<MediaItem>();

        switch (kind)
        {
            case "album":
                {
                    var id = ExtractNumericId(segments);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        var album = await _apiClient.GetAlbumMetaAsync(id, ct);
                        if (album?.Response is not null)
                        {
                            title = album.Response.ContainerInfo;
                            artist = album.Response.ArtistName;
                            metadata["albumId"] = id;
                            metadata["containerType"] = album.Response.ContainerTypeStr ?? string.Empty;
                            items = album.Response.Tracks.Select((track, index) => new MediaItem(
                                Id: track.TrackID.ToString(),
                                DisplayName: track.SongTitle,
                                Kind: "audio",
                                Index: index,
                                Metadata: new Dictionary<string, string>
                                {
                                    ["trackId"] = track.TrackID.ToString(),
                                    ["title"] = track.SongTitle
                                })).ToArray();
                        }
                    }
                    break;
                }
            case "artist":
                {
                    var id = ExtractNumericId(segments);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        var artistMeta = await _apiClient.GetArtistMetaAsync(id, ct);
                        var firstContainer = artistMeta.FirstOrDefault()?.Response?.Containers.FirstOrDefault();
                        if (firstContainer is not null)
                        {
                            title = firstContainer.ArtistName;
                            artist = firstContainer.ArtistName;
                            metadata["artistId"] = id;
                            items = firstContainer.Tracks.Select((track, index) => new MediaItem(
                                Id: track.TrackID.ToString(),
                                DisplayName: track.SongTitle,
                                Kind: "audio",
                                Index: index,
                                Metadata: new Dictionary<string, string>
                                {
                                    ["trackId"] = track.TrackID.ToString(),
                                    ["title"] = track.SongTitle
                                })).ToArray();
                        }
                    }
                    break;
                }
            case "playlist":
                {
                    var id = ExtractNumericId(segments);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        var playlist = await _apiClient.GetPlaylistMetaAsync(id, string.Empty, string.Empty, false, ct);
                        if (playlist?.Response is not null)
                        {
                            title = playlist.Response.PlayListName;
                            metadata["playlistId"] = id;
                            items = playlist.Response.Items.Select((item, index) => new MediaItem(
                                Id: item.Track.TrackID.ToString(),
                                DisplayName: item.Track.SongTitle,
                                Kind: "audio",
                                Index: index,
                                Metadata: new Dictionary<string, string>
                                {
                                    ["trackId"] = item.Track.TrackID.ToString(),
                                    ["title"] = item.Track.SongTitle
                                })).ToArray();
                        }
                    }
                    break;
                }
            case "video":
                {
                    var id = ExtractNumericId(segments);
                    metadata["videoId"] = id ?? uri.AbsoluteUri;
                    title = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? uri.Host);
                    items = new[]
                    {
                        new MediaItem(uri.ToString(), title, "video", 0, metadata)
                    };
                    break;
                }
            case "livestream":
                {
                    var id = ExtractNumericId(segments);
                    metadata["livestreamId"] = id ?? uri.AbsoluteUri;
                    title = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? uri.Host);
                    items = new[]
                    {
                        new MediaItem(uri.ToString(), title, "livestream", 0, metadata)
                    };
                    break;
                }
            default:
                {
                    title = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? uri.Host);
                    items = new[]
                    {
                        new MediaItem(uri.ToString(), title, "album", 0, metadata)
                    };
                    break;
                }
        }

        if (items.Count == 0)
        {
            items = new[]
            {
                new MediaItem(uri.ToString(), title, kind, 0, metadata)
            };
        }

        return new MediaDiscoveryResult(
            ProviderId: Id,
            SourceUrl: uri,
            CanonicalUrl: uri,
            Title: title,
            ArtistName: artist,
            Items: items,
            HasVideo: items.Any(item => item.Kind is "video" or "livestream"),
            HasAudio: items.Any(item => item.Kind is "audio"),
            Metadata: metadata);
    }

    public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct)
    {
        var selectedItems = SelectItems(discovery, preferences);
        var expectedFiles = selectedItems.Select(item =>
        {
            var fileKind = item.Kind switch
            {
                "video" or "livestream" => FileKind.Video,
                "metadata" => FileKind.Metadata,
                _ => FileKind.Audio
            };

            var nameParts = new List<string>
            {
                SanitizePathSegment(discovery.Title)
            };

            if (item.Metadata.TryGetValue("trackNumber", out var trackNumber) && !string.IsNullOrWhiteSpace(trackNumber))
            {
                nameParts.Add(trackNumber.PadLeft(2, '0'));
            }

            nameParts.Add(SanitizePathSegment(item.DisplayName));

            if (fileKind == FileKind.Audio && !string.IsNullOrWhiteSpace(preferences.PreferredAudioFormat))
            {
                nameParts.Add(SanitizePathSegment(preferences.PreferredAudioFormat));
            }

            if (fileKind == FileKind.Video && !string.IsNullOrWhiteSpace(preferences.PreferredVideoResolution))
            {
                nameParts.Add(SanitizePathSegment(preferences.PreferredVideoResolution));
            }

            return new FileState(
                Guid.NewGuid(),
                Guid.Empty,
                Path.Combine(new[] { preferences.OutputRoot }.Concat(nameParts).ToArray()),
                fileKind,
                FileStatus.Partial,
                0,
                0,
                null,
                null);
        }).ToList();

        if (preferences.WriteMetadata)
        {
            expectedFiles.Add(new FileState(
                Guid.NewGuid(),
                Guid.Empty,
                Path.Combine(preferences.OutputRoot, SanitizePathSegment(discovery.Title), "metadata.nfo"),
                FileKind.Metadata,
                FileStatus.Partial,
                0,
                0,
                null,
                null));
        }

        if (preferences.WriteArtwork)
        {
            expectedFiles.Add(new FileState(
                Guid.NewGuid(),
                Guid.Empty,
                Path.Combine(preferences.OutputRoot, SanitizePathSegment(discovery.Title), "cover.jpg"),
                FileKind.Artwork,
                FileStatus.Partial,
                0,
                0,
                null,
                null));
        }

        return Task.FromResult(new DownloadPlan(
            ProviderId: Id,
            JobId: Guid.NewGuid(),
            Items: selectedItems,
            OutputRoot: preferences.OutputRoot,
            Preferences: preferences,
            ExpectedFiles: expectedFiles,
            ResumeState: discovery.Metadata));
    }

    public Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalItems = Math.Max(plan.Items.Count, plan.ExpectedFiles.Count);
        if (totalItems == 0)
        {
            progress.Report(new DownloadProgress(plan.JobId, plan.ProviderId, 0, 0, 100, 0, 0, $"No items to download for {plan.ProviderId}."));
            return Task.CompletedTask;
        }

        for (var index = 0; index < totalItems; index++)
        {
            ct.ThrowIfCancellationRequested();

            var currentItem = index + 1;
            var percent = (int)Math.Round((double)currentItem / totalItems * 100, MidpointRounding.AwayFromZero);
            progress.Report(new DownloadProgress(
                plan.JobId,
                plan.ProviderId,
                currentItem,
                totalItems,
                percent,
                currentItem,
                plan.Items.Count,
                $"Prepared {currentItem}/{totalItems} item(s) for {plan.ProviderId}."));
        }

        return Task.CompletedTask;
    }

    private static string ResolveKind(string[] segments)
    {
        if (segments.Length == 0)
        {
            return "album";
        }

        if (segments.Any(segment => segment.Equals("videos", StringComparison.OrdinalIgnoreCase)))
        {
            return "video";
        }

        if (segments.Any(segment => segment.Equals("watch", StringComparison.OrdinalIgnoreCase)))
        {
            return "livestream";
        }

        if (segments.Any(segment => AlbumSegments.Contains(segment)))
        {
            return "album";
        }

        if (segments.Any(segment => ArtistSegments.Contains(segment)))
        {
            return "artist";
        }

        if (segments.Any(segment => segment.Equals("playlists", StringComparison.OrdinalIgnoreCase) || segment.Equals("playlist", StringComparison.OrdinalIgnoreCase)))
        {
            return "playlist";
        }

        return "album";
    }

    private static string ExtractNumericId(string[] segments) =>
        segments.LastOrDefault(segment => segment.All(char.IsDigit)) ?? string.Empty;

    private static IReadOnlyList<MediaItem> SelectItems(MediaDiscoveryResult discovery, DownloadPreferences preferences)
    {
        var items = discovery.Items;

        if (preferences.SkipVideos)
        {
            items = items.Where(item => item.Kind is not "video" and not "livestream").ToArray();
        }
        else if (preferences.ForceVideo && discovery.HasVideo)
        {
            var videoItems = items.Where(item => item.Kind is "video" or "livestream").ToArray();
            if (videoItems.Length > 0)
            {
                items = videoItems;
            }
        }

        var selected = items.Select(item =>
        {
            var metadata = new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["providerId"] = discovery.ProviderId,
                ["contentKind"] = item.Kind
            };

            if (item.Kind is "audio")
            {
                metadata["selectedAudioFormat"] = preferences.PreferredAudioFormat ?? "auto";
            }

            if (item.Kind is "video" or "livestream")
            {
                metadata["selectedVideoResolution"] = preferences.PreferredVideoResolution ?? "auto";
            }

            if (!metadata.ContainsKey("trackNumber"))
            {
                metadata["trackNumber"] = (item.Index + 1).ToString();
            }

            return item with
            {
                Metadata = metadata
            };
        }).ToArray();

        return selected;
    }

    private static string SanitizePathSegment(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "untitled" : value.Trim();
    }
}
