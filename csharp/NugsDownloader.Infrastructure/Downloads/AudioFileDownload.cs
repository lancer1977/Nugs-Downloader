namespace NugsDownloader.Infrastructure.Downloads;

internal static class AudioFileDownload
{
    public static async Task<string> WriteAsync(Uri mediaUri, string targetPath,
        Func<Uri, Stream, CancellationToken, Task<string?>> download, CancellationToken ct)
    {
        var partialPath = targetPath + ".partial";
        try
        {
            string? contentType;
            await using (var destination = File.Create(partialPath))
            {
                contentType = await download(mediaUri, destination, ct);
            }

            if (new FileInfo(partialPath).Length == 0)
            {
                throw new InvalidOperationException("Downloaded audio file was empty.");
            }

            var extension = await AudioFileName.FromFileAsync(partialPath, ct)
                ?? AudioFileName.FromContentType(contentType)
                ?? AudioFileName.FromUri(mediaUri)
                ?? ".bin";
            targetPath = AudioFileName.WithExtension(targetPath, extension);
            File.Move(partialPath, targetPath, overwrite: true);
            return targetPath;
        }
        catch
        {
            if (File.Exists(partialPath)) File.Delete(partialPath);
            throw;
        }
    }
}
