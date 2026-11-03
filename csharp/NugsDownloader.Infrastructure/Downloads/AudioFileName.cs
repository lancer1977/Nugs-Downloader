namespace NugsDownloader.Infrastructure.Downloads;

internal static class AudioFileName
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".m4a", ".mp4", ".aac", ".flac", ".mp3", ".wav", ".ogg", ".opus", ".aif", ".aiff"
    };

    public static string? FromUri(Uri? uri)
    {
        return uri is null ? null : FromName(uri.AbsolutePath);
    }

    public static string? FromName(string name)
    {
        var extension = Path.GetExtension(name);
        return Extensions.Contains(extension) ? extension.ToLowerInvariant() : null;
    }

    public static async Task<string?> FromFileAsync(string path, CancellationToken ct)
    {
        var header = new byte[16];
        await using var stream = File.OpenRead(path);
        var length = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken: ct);
        if (length >= 4 && header.AsSpan(0, 4).SequenceEqual("fLaC"u8)) return ".flac";
        if (length >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WAVE"u8)) return ".wav";
        if (length >= 12 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            if (header.AsSpan(8, 4).SequenceEqual("M4A "u8)) return ".m4a";
            if (header.AsSpan(8, 4).SequenceEqual("isom"u8)) return ".mp4";
            // An ftyp box alone also occurs in non-audio containers (e.g. HEIF).
            return null;
        }
        if (length >= 2 && header[0] == 0xff && (header[1] == 0xf1 || header[1] == 0xf9)) return ".aac";
        return null;
    }

    public static string? FromContentType(string? contentType) => contentType switch
    {
        "audio/mp4" => ".m4a",
        "audio/flac" => ".flac",
        "audio/mpeg" => ".mp3",
        _ => null
    };

    public static string WithExtension(string name, string extension)
    {
        var existing = Path.GetExtension(name);
        return Extensions.Contains(existing) || existing.Equals(".bin", StringComparison.OrdinalIgnoreCase)
            ? name[..^existing.Length] + extension : name + extension;
    }
}
