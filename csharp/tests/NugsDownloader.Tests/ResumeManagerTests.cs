using NugsDownloader.Infrastructure.Downloads;
using Xunit;

namespace NugsDownloader.Tests;

public class ResumeManagerTests
{
    [Fact]
    public void CreateInitialState_PopulatesCoreFields()
    {
        var state = FileResumeManager.CreateInitialState("/tmp/test.mp3", "http://example.com/file.mp3", 1000, "etag123");

        Assert.Equal("/tmp/test.mp3", state.FilePath);
        Assert.Equal("http://example.com/file.mp3", state.Url);
        Assert.Equal(1000, state.TotalSize);
        Assert.Equal(0, state.DownloadedSize);
        Assert.Equal("etag123", state.ETag);
    }

    [Fact]
    public async Task SaveLoadDeleteState_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var manager = new FileResumeManager(dir);
        var state = FileResumeManager.CreateInitialState("/tmp/test.mp3", "http://example.com/file.mp3", 1000, "etag123") with
        {
            DownloadedSize = 500,
            Checksum = "abc123"
        };

        await manager.SaveStateAsync(state, CancellationToken.None);

        var loaded = await manager.LoadStateAsync(state.FilePath, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(500, loaded!.DownloadedSize);
        Assert.Equal("abc123", loaded.Checksum);

        await manager.DeleteStateAsync(state.FilePath, CancellationToken.None);
        Assert.Null(await manager.LoadStateAsync(state.FilePath, CancellationToken.None));
    }

    [Fact]
    public void ValidatePartialDownload_AllowsMatchingFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "partial.mp3");
        File.WriteAllBytes(file, new byte[500]);
        var state = FileResumeManager.CreateInitialState(file, "http://example.com/file.mp3", 1000, "etag123") with
        {
            DownloadedSize = 500
        };

        FileResumeManager.ValidatePartialDownload(state);
    }

    [Fact]
    public void ValidatePartialDownload_RejectsMissingFile()
    {
        var state = FileResumeManager.CreateInitialState("/nonexistent/file.mp3", "http://example.com/file.mp3", 1000, "etag123") with
        {
            DownloadedSize = 500
        };

        var ex = Assert.Throws<InvalidOperationException>(() => FileResumeManager.ValidatePartialDownload(state));
        Assert.Contains("partial file no longer exists", ex.Message);
    }

    [Fact]
    public void ValidatePartialDownload_RejectsSizeMismatch()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "partial.mp3");
        File.WriteAllBytes(file, new byte[400]);
        var state = FileResumeManager.CreateInitialState(file, "http://example.com/file.mp3", 1000, "etag123") with
        {
            DownloadedSize = 500
        };

        var ex = Assert.Throws<InvalidOperationException>(() => FileResumeManager.ValidatePartialDownload(state));
        Assert.Contains("file size mismatch", ex.Message);
    }

    [Fact]
    public void ValidatePartialDownload_RejectsOldState()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "partial.mp3");
        File.WriteAllBytes(file, new byte[500]);
        var state = FileResumeManager.CreateInitialState(file, "http://example.com/file.mp3", 1000, "etag123") with
        {
            DownloadedSize = 500,
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-25)
        };

        var ex = Assert.Throws<InvalidOperationException>(() => FileResumeManager.ValidatePartialDownload(state));
        Assert.Contains("resume state is too old", ex.Message);
    }

    [Fact]
    public void CalculateChecksumFromBytes_ReturnsStableMd5()
    {
        var checksum = FileResumeManager.CalculateChecksumFromBytes(System.Text.Encoding.UTF8.GetBytes("test data for checksum"));
        Assert.Equal("a16de13eaa4650a7827e619b6db9fcb7", checksum);
    }
}
