using Microsoft.Extensions.Options;
using NugsDownloader.Web.Options;
using NugsDownloader.Web.Services;
using Xunit;

namespace NugsDownloader.Tests;

public class SqliteStorePathsTests
{
    [Fact]
    public void SqliteStorePathsPointsToStateDatabaseFile()
    {
        var options = Options.Create(new NugsDownloaderStorageOptions
        {
            StateDirectory = Path.Combine(Path.GetTempPath(), "nugs-state-db")
        });

        var paths = new SqliteStorePaths(options);

        Assert.Equal(Path.Combine(paths.BaseDirectory, "nugs-downloader.db"), paths.DatabasePath);
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nugs-state-db")), paths.BaseDirectory);
    }
}
