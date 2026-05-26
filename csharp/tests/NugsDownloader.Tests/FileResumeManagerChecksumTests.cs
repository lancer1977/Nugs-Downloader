using NugsDownloader.Infrastructure.Downloads;

using Xunit;

namespace NugsDownloader.Tests;

public class FileResumeManagerChecksumTests
{
    [Fact]
    public void CalculateChecksumFromBytes_IsDeterministicForIdenticalInput()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };

        var first = FileResumeManager.CalculateChecksumFromBytes(bytes);
        var second = FileResumeManager.CalculateChecksumFromBytes(bytes);

        Assert.Equal("08d6c05a21512a79a1dfeb9d2a8f262f", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void CalculateChecksumFromBytes_DiffersForDifferentInput()
    {
        var left = new byte[] { 1, 2, 3, 4 };
        var right = new byte[] { 1, 2, 3, 5 };

        Assert.NotEqual(FileResumeManager.CalculateChecksumFromBytes(left), FileResumeManager.CalculateChecksumFromBytes(right));
    }
}
