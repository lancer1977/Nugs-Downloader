using NugsDownloader.Web.Options;
using Xunit;

namespace NugsDownloader.Tests;

public class NugsDownloaderStorageOptionsTests
{
    [Fact]
    public void GetStateDirectory_NormalizesRelativeStatePath()
    {
        var options = new NugsDownloaderStorageOptions
        {
            StateDirectory = "relative-state"
        };

        var value = options.GetStateDirectory();

        Assert.True(Path.IsPathRooted(value));
        Assert.EndsWith(Path.Combine("relative-state"), value);
    }

    [Fact]
    public void GetDownloadDirectory_PreservesAbsoluteDownloadPath()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "nugs-downloads");
        var options = new NugsDownloaderStorageOptions
        {
            DownloadDirectory = absolute
        };

        Assert.Equal(absolute, options.GetDownloadDirectory());
    }
}
