using NugsDownloader.Domain.Entities;

using Xunit;

namespace NugsDownloader.Tests;

public class DownloadJobStatusTests
{
    [Fact]
    public void DownloadJobStatus_ContainsExpectedLifecycleStates()
    {
        var statuses = new[]
        {
            DownloadJobStatus.Pending,
            DownloadJobStatus.Discovering,
            DownloadJobStatus.Ready,
            DownloadJobStatus.Running,
            DownloadJobStatus.Paused,
            DownloadJobStatus.Completed,
            DownloadJobStatus.Failed,
            DownloadJobStatus.Cancelled
        };

        Assert.Equal(8, statuses.Length);
        Assert.Contains(DownloadJobStatus.Ready, statuses);
        Assert.Contains(DownloadJobStatus.Completed, statuses);
    }
}
