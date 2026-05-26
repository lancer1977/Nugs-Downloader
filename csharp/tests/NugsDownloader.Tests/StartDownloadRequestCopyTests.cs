using NugsDownloader.App.UseCases;
using NugsDownloader.Domain.ValueObjects;

using Xunit;

namespace NugsDownloader.Tests;

public class StartDownloadRequestCopyTests
{
    [Fact]
    public void StartDownloadRequest_with_updates_request_state()
    {
        var request = new StartDownloadRequest(
            Guid.Parse("22222222-1111-1111-1111-111111111111"),
            "nugs",
            new Uri("https://play.nugs.net/show/1"),
            new Credentials("alice", "pwd", "token"),
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true));

        var withProvider = request with { ProviderId = "livephish" };

        Assert.Equal("nugs", request.ProviderId);
        Assert.Equal("livephish", withProvider.ProviderId);
        Assert.Equal(request.JobId, withProvider.JobId);
        Assert.Equal(request.SourceUrl, withProvider.SourceUrl);
    }
}
