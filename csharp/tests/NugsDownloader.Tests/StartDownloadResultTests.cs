using NugsDownloader.App.UseCases;
using Xunit;

namespace NugsDownloader.Tests;

public class StartDownloadResultTests
{
    [Fact]
    public void StartDownloadResult_ExposesStatusPayload()
    {
        var result = new StartDownloadResult(
            JobId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ProviderId: "nugs",
            Status: "Completed",
            Message: "all good");

        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), result.JobId);
        Assert.Equal("nugs", result.ProviderId);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("all good", result.Message);
    }
}
