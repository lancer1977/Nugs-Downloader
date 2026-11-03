using System.Buffers.Binary;

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
        // Bound both I/O and box traversal; never allocate or seek based on box sizes.
        var header = new byte[4096];
        await using var stream = File.OpenRead(path);
        var length = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken: ct);
        if (length >= 4 && header.AsSpan(0, 4).SequenceEqual("fLaC"u8)) return ".flac";
        if (length >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WAVE"u8)) return ".wav";
        if (length >= 2 && header[0] == 0xff && (header[1] == 0xf1 || header[1] == 0xf9)) return ".aac";
        return FromIsoBmff(header.AsSpan(0, length));
    }

    private static string? FromIsoBmff(ReadOnlySpan<byte> header)
    {
        var offset = 0;
        for (var count = 0; count < 32 && header.Length - offset >= 8; count++)
        {
            var box = header[offset..];
            var type = box.Slice(4, 4);
            var isFileType = type.SequenceEqual("ftyp"u8);
            if (!isFileType && !type.SequenceEqual("free"u8)
                && !type.SequenceEqual("skip"u8) && !type.SequenceEqual("wide"u8)) return offset == 0 ? null : ".bin";
            var headerSize = 8;
            ulong size = BinaryPrimitives.ReadUInt32BigEndian(box);
            if (size == 1)
            {
                if (box.Length < 16) return ".bin";
                size = BinaryPrimitives.ReadUInt64BigEndian(box.Slice(8, 8));
                headerSize = 16;
            }
            // Zero (to EOF), truncated and over-budget boxes are deliberately inconclusive.
            // .bin is an explicit veto: contradictory MIME/URL metadata must not win.
            if (size < (ulong)headerSize || size > (ulong)box.Length) return ".bin";
            if (isFileType)
            {
                if (size < (ulong)(headerSize + 8) || (size - (ulong)headerSize - 8) % 4 != 0) return ".bin";
                string? extension = null;
                // Image brands veto even a generic MP4-compatible brand and audio metadata.
                for (var i = headerSize; i < (int)size; i += 4)
                {
                    if (i == headerSize + 4) continue; // minor_version is not a brand
                    var brand = box.Slice(i, 4);
                    if (IsImageBrand(brand)) return ".bin";
                    if (brand.SequenceEqual("M4A "u8)) extension = ".m4a";
                    else if (IsMp4Brand(brand)) extension ??= ".mp4";
                }
                return extension ?? ".bin";
            }
            offset += (int)size;
        }
        return offset == 0 ? null : ".bin";
    }

    private static bool IsMp4Brand(ReadOnlySpan<byte> brand) =>
        brand.SequenceEqual("isom"u8) || brand.SequenceEqual("iso2"u8)
        || brand.SequenceEqual("mp41"u8) || brand.SequenceEqual("mp42"u8);

    private static bool IsImageBrand(ReadOnlySpan<byte> brand) =>
        brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8)
        || brand.SequenceEqual("hevc"u8) || brand.SequenceEqual("hevx"u8)
        || brand.SequenceEqual("heim"u8) || brand.SequenceEqual("heis"u8)
        || brand.SequenceEqual("hevm"u8) || brand.SequenceEqual("hevs"u8)
        || brand.SequenceEqual("mif1"u8) || brand.SequenceEqual("msf1"u8)
        || brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);

    public static string? FromContentType(string? contentType) => contentType?.ToLowerInvariant() switch
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
