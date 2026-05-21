namespace NugsDownloader.Web.Options;

public sealed class NugsDownloaderStorageOptions
{
    public string StateDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "state");
    public string DownloadDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "downloads");

    public string GetStateDirectory() => Normalize(StateDirectory);

    public string GetDownloadDirectory() => Normalize(DownloadDirectory);

    private static string Normalize(string path) =>
        Path.IsPathRooted(path) ? path : Path.GetFullPath(path, AppContext.BaseDirectory);
}
