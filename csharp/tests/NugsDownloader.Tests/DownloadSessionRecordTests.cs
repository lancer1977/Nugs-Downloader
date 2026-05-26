using NugsDownloader.Domain.Entities;

using Xunit;

namespace NugsDownloader.Tests;

public class DownloadSessionRecordTests
{
    [Fact]
    public void DownloadSession_RecordsDownloadProgressMetadata()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var progressAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var session = new DownloadSession(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "nugs",
            startedAt,
            progressAt,
            128,
            512,
            25,
            1,
            4);

        Assert.Equal("nugs", session.ProviderId);
        Assert.Equal(startedAt, session.StartedAt);
        Assert.Equal(progressAt, session.LastProgressAt);
        Assert.Equal(25, session.PercentComplete);
        Assert.Equal(128, session.DownloadedBytes);
        Assert.Equal(512, session.TotalBytes);
    }
}
