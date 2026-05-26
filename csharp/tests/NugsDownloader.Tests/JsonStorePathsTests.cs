using Microsoft.Extensions.Options;
using NugsDownloader.Web.Options;
using NugsDownloader.Web.Services;
using Xunit;

namespace NugsDownloader.Tests;

public class JsonStorePathsTests
{
    [Fact]
    public void JsonStorePathsBuildsExpectedFilenames()
    {
        var options = Options.Create(new NugsDownloaderStorageOptions
        {
            StateDirectory = Path.Combine(Path.GetTempPath(), "nugs-state")
        });

        var paths = new JsonStorePaths(options);
        var expectedBase = options.Value.GetStateDirectory();

        Assert.Equal(Path.Combine(expectedBase, "jobs.json"), paths.Jobs);
        Assert.Equal(Path.Combine(expectedBase, "file-states.json"), paths.FileStates);
        Assert.Equal(Path.Combine(expectedBase, "credentials.json"), paths.Credentials);
        Assert.Equal(Path.Combine(expectedBase, "secrets.json"), paths.Secrets);
    }
}
