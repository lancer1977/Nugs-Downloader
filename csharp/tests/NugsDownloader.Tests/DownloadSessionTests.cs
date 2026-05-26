using NugsDownloader.Domain.Entities;
using Xunit;

namespace NugsDownloader.Tests;

public class DownloadSessionTests
{
    [Fact]
    public void DownloadSession_StoresValues()
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-5);
        var lastProgress = DateTimeOffset.UtcNow;
        var session = new DownloadSession(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "nugs",
            started,
            lastProgress,
            5000,
            10000,
            50,
            4,
            8);

        Assert.Equal("nugs", session.ProviderId);
        Assert.Equal(5000, session.DownloadedBytes);
        Assert.Equal(10000, session.TotalBytes);
        Assert.Equal(lastProgress, session.LastProgressAt);
        Assert.Equal(50, session.PercentComplete);
    }

    [Fact]
    public void DownloadSession_WithoutLastProgressAllowsNull()
    {
        var session = new DownloadSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "nugs",
            DateTimeOffset.UtcNow,
            null,
            0,
            0,
            0,
            1,
            1);

        Assert.Null(session.LastProgressAt);
        Assert.Equal(1, session.CurrentItem);
    }
}
