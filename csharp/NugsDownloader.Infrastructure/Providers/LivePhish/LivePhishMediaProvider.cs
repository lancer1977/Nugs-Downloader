using System.Text;
using System.Text.RegularExpressions;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.Providers;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Infrastructure.Providers.LivePhish;

public sealed class LivePhishMediaProvider : ILivePhishProvider
{
    private static readonly Regex[] RecordingPatterns =
    {
        new(@"^https://plus\.livephish\.com/(?:index\.html|)#/catalog/recording/(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^https://www\.livephish\.com/browse/music/0,(\d+)/[\w-]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    private readonly LivePhishApiClient _apiClient;

    public LivePhishMediaProvider()
        : this(new LivePhishApiClient())
    {
    }

    public LivePhishMediaProvider(LivePhishApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public string Id => "livephish";

    public string DisplayName => "LivePhish";

    public ProviderCapabilities Capabilities { get; } = new(
        SupportsAudio: true,
        SupportsVideo: false,
        SupportsChapters: false,
        SupportsResume: true,
        SupportsTokens: true,
        SupportsPasswordLogin: true,
        SupportedFormats: new[] { "alac", "flac", "aac" },
        SupportedResolutions: Array.Empty<string>());

    public bool CanHandle(Uri uri)
    {
        return string.Equals(uri.Host, "livephish.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".livephish.com", StringComparison.OrdinalIgnoreCase);
    }

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
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = uri.Host,
            ["path"] = uri.AbsolutePath,
            ["url"] = uri.AbsoluteUri
        };

        var recordingId = ExtractRecordingId(uri.AbsoluteUri);
        if (!string.IsNullOrWhiteSpace(recordingId))
        {
            metadata["recordingId"] = recordingId;
        }

        string title = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? uri.Host);
        string? artist = null;
        IReadOnlyList<MediaItem> items = Array.Empty<MediaItem>();

        if (!string.IsNullOrWhiteSpace(recordingId))
        {
            var album = await _apiClient.GetAlbumMetaAsync(recordingId, ct);
            if (album?.Response is not null)
            {
                title = album.Response.ContainerInfo;
                artist = album.Response.ArtistName;
                metadata["containerType"] = album.Response.ContainerTypeStr ?? string.Empty;
                items = album.Response.Tracks.Select((track, index) => new MediaItem(
                    Id: track.TrackID.ToString(),
                    DisplayName: track.SongTitle,
                    Kind: "audio",
                    Index: index,
                    Metadata: BuildTrackMetadata(track, index))).ToArray();
            }
        }

        if (items.Count == 0)
        {
            items = new[]
            {
                new MediaItem(uri.ToString(), title, "audio", 0, metadata)
            };
        }

        return new MediaDiscoveryResult(
            ProviderId: Id,
            SourceUrl: uri,
            CanonicalUrl: uri,
            Title: title,
            ArtistName: artist,
            Items: items,
            HasVideo: false,
            HasAudio: items.Any(item => item.Kind is "audio"),
            Metadata: metadata);
    }

    public Task<DownloadPlan> BuildDownloadPlanAsync(MediaDiscoveryResult discovery, DownloadPreferences preferences, CancellationToken ct)
    {
        var selectedItems = SelectItems(discovery, preferences);
        var baseFolder = BuildBaseFolder(discovery, preferences);
        var expectedFiles = selectedItems.Select(item =>
        {
            var filePath = Path.Combine(baseFolder, BuildTrackFileName(item, preferences.PreferredAudioFormat));
            return new FileState(
                Guid.NewGuid(),
                Guid.Empty,
                filePath,
                FileKind.Audio,
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
                Path.Combine(baseFolder, "metadata.nfo"),
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
                Path.Combine(baseFolder, "cover.jpg"),
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

    public async Task ExecuteDownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalItems = Math.Max(plan.Items.Count, plan.ExpectedFiles.Count);
        if (totalItems == 0)
        {
            progress.Report(new DownloadProgress(plan.JobId, plan.ProviderId, 0, 0, 100, 0, 0, $"No items to download for {plan.ProviderId}."));
            return;
        }

        for (var index = 0; index < totalItems; index++)
        {
            ct.ThrowIfCancellationRequested();

            if (index < plan.ExpectedFiles.Count)
            {
                await WriteExpectedFileAsync(plan, index, ct);
            }

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
                $"Downloaded {currentItem}/{totalItems} file(s) for {plan.ProviderId}."));
        }
    }

    private async Task WriteExpectedFileAsync(DownloadPlan plan, int index, CancellationToken ct)
    {
        var expectedFile = plan.ExpectedFiles[index];
        var item = index < plan.Items.Count ? plan.Items[index] : null;
        var targetPath = Path.GetFullPath(expectedFile.FilePath);
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        switch (expectedFile.Kind)
        {
            case FileKind.Audio:
                var mediaUri = ResolveMediaUri(item);
                if (mediaUri is null)
                {
                    throw new InvalidOperationException($"No media URL is available for LivePhish audio item {item?.Id ?? index.ToString()}.");
                }

                await DownloadMediaFileAtomicallyAsync(mediaUri, targetPath, ct);
                break;
            case FileKind.Metadata:
                await WriteTextFileAtomicallyAsync(targetPath, BuildMetadataFile(plan), ct);
                break;
            case FileKind.Artwork:
                await WriteTextFileAtomicallyAsync(targetPath, "Artwork unavailable for this LivePhish item.", ct);
                break;
        }
    }

    private async Task DownloadMediaFileAtomicallyAsync(Uri mediaUri, string targetPath, CancellationToken ct)
    {
        var partialPath = targetPath + ".partial";
        try
        {
            await using (var destination = File.Create(partialPath))
            {
                await _apiClient.DownloadFileAsync(mediaUri, destination, ct);
            }

            var downloaded = new FileInfo(partialPath);
            if (!downloaded.Exists || downloaded.Length <= 0)
            {
                throw new InvalidOperationException("Downloaded LivePhish media file was empty.");
            }

            File.Move(partialPath, targetPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }

            throw;
        }
    }

    private static async Task WriteTextFileAtomicallyAsync(string targetPath, string contents, CancellationToken ct)
    {
        var partialPath = targetPath + ".partial";
        try
        {
            await File.WriteAllTextAsync(partialPath, contents, Encoding.UTF8, ct);
            File.Move(partialPath, targetPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }

            throw;
        }
    }

    private static Uri? ResolveMediaUri(MediaItem? item)
    {
        if (item?.Metadata is not IReadOnlyDictionary<string, string> values)
        {
            return null;
        }

        foreach (var key in new[] { "downloadUrl", "mediaUrl", "streamUrl", "clipUrl", "clipURL", "url" })
        {
            if (values.TryGetValue(key, out var value) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    private static string BuildMetadataFile(DownloadPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LivePhish download metadata");
        builder.AppendLine($"Provider: {plan.ProviderId}");
        builder.AppendLine($"Items: {plan.Items.Count}");
        foreach (var item in plan.Items)
        {
            builder.AppendLine($"- {item.Index + 1}. {item.DisplayName} ({item.Kind})");
        }

        return builder.ToString();
    }

    private static string ExtractRecordingId(string value)
    {
        foreach (var pattern in RecordingPatterns)
        {
            var match = pattern.Match(value);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return string.Empty;
    }

    private static string BuildBaseFolder(MediaDiscoveryResult discovery, DownloadPreferences preferences)
    {
        var parts = new List<string> { preferences.OutputRoot };

        if (!string.IsNullOrWhiteSpace(discovery.ArtistName))
        {
            parts.Add(SanitizePathSegment(discovery.ArtistName));
        }

        parts.Add(SanitizePathSegment(discovery.Title));
        return Path.Combine(parts.ToArray());
    }

    private static string BuildTrackFileName(MediaItem item, string? preferredAudioFormat)
    {
        var trackNumber = item.Metadata.TryGetValue("trackNumber", out var rawTrackNumber) && !string.IsNullOrWhiteSpace(rawTrackNumber)
            ? rawTrackNumber.PadLeft(2, '0')
            : (item.Index + 1).ToString("D2");

        var extension = string.IsNullOrWhiteSpace(preferredAudioFormat)
            ? string.Empty
            : preferredAudioFormat.StartsWith('.')
                ? preferredAudioFormat
                : "." + SanitizePathSegment(preferredAudioFormat);

        return $"{trackNumber}. {SanitizePathSegment(item.DisplayName)}{extension}";
    }

    private static IReadOnlyDictionary<string, string> BuildTrackMetadata(LivePhishApiClient.Track track, int index)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["trackId"] = track.TrackID.ToString(),
            ["title"] = track.SongTitle,
            ["trackNumber"] = (index + 1).ToString()
        };

        if (!string.IsNullOrWhiteSpace(track.ClipURL))
        {
            metadata["clipUrl"] = track.ClipURL;
        }

        return metadata;
    }

    private static IReadOnlyList<MediaItem> SelectItems(MediaDiscoveryResult discovery, DownloadPreferences preferences)
    {
        var items = discovery.Items.Where(item => item.Kind is not "video" and not "livestream").ToArray();

        var selected = items.Select(item =>
        {
            var metadata = new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["providerId"] = discovery.ProviderId,
                ["contentKind"] = item.Kind,
                ["selectedAudioFormat"] = preferences.PreferredAudioFormat ?? "auto"
            };

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