using Microsoft.Extensions.Options;
using NugsDownloader.Web.Options;

namespace NugsDownloader.Web.Services;

public sealed class SqliteStorePaths
{
    public SqliteStorePaths(IOptions<NugsDownloaderStorageOptions> options)
    {
        BaseDirectory = options.Value.GetStateDirectory();
        DatabasePath = Path.Combine(BaseDirectory, "nugs-downloader.db");
    }

    public string BaseDirectory { get; }
    public string DatabasePath { get; }
}
