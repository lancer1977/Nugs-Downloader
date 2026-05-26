using NugsDownloader.Domain.Entities;
using Xunit;

namespace NugsDownloader.Tests;

public class FileStateTests
{
    [Fact]
    public void FileState_StoresValues()
    {
        var fileState = new FileState(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "/tmp/test.flac",
            FileKind.Audio,
            FileStatus.Partial,
            ExpectedSize: 1000,
            ActualSize: 250,
            Checksum: "sha256:abcd",
            LastVerifiedAt: DateTimeOffset.UtcNow);

        Assert.Equal("/tmp/test.flac", fileState.FilePath);
        Assert.Equal(FileKind.Audio, fileState.Kind);
        Assert.Equal(FileStatus.Partial, fileState.Status);
        Assert.Equal(250, fileState.ActualSize);
        Assert.Equal("sha256:abcd", fileState.Checksum);
    }

    [Fact]
    public void FileState_WithReturnsUpdatedCopy()
    {
        var fileState = new FileState(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "/tmp/test.flac",
            FileKind.Audio,
            FileStatus.Missing,
            1000,
            0,
            null,
            null);

        var complete = fileState with { Status = FileStatus.Complete };

        Assert.Equal(FileStatus.Complete, complete.Status);
        Assert.Equal(fileState.FilePath, complete.FilePath);
    }
}
