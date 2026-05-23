using Microsoft.Extensions.Options;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Web.Options;
using NugsDownloader.Web.Services;
using Xunit;

namespace NugsDownloader.Tests;

public class SqliteRepositoryTests
{
    [Fact]
    public async Task SqliteRepositories_RoundTripJobsFilesCredentialsAndSecrets()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = Options.Create(new NugsDownloaderStorageOptions { StateDirectory = stateDirectory });
        var paths = new SqliteStorePaths(options);
        var store = new SqliteStateStore(paths);
        var jobs = new SqliteJobRepository(store);
        var states = new SqliteFileStateRepository(store);
        var creds = new SqliteCredentialStore(store);
        var vault = new SqliteSecretVault(store);

        var jobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var job = new DownloadJob(
            jobId,
            "nugs",
            new Uri("https://example.com/release/1"),
            "Demo",
            DownloadJobStatus.Running,
            DateTimeOffset.Parse("2026-05-22T10:00:00.0000000+00:00"),
            DateTimeOffset.Parse("2026-05-22T10:00:05.0000000+00:00"),
            null,
            null,
            "Downloads",
            "Main",
            "alice",
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true));

        var fileState = new FileState(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            jobId,
            "Downloads/Demo/file.flac",
            FileKind.Audio,
            FileStatus.Partial,
            100,
            50,
            null,
            DateTimeOffset.Parse("2026-05-22T10:01:00.0000000+00:00"));

        var account = new ProviderAccount(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "nugs",
            "Main",
            "alice",
            "nugs:Main",
            AuthenticationState.Valid,
            DateTimeOffset.Parse("2026-05-22T10:02:00.0000000+00:00"));

        var secretRef = await vault.StoreAsync("nugs", "Main", "secret-value", CancellationToken.None);
        await jobs.SaveAsync(job, CancellationToken.None);
        await states.SaveAsync(fileState, CancellationToken.None);
        await creds.SaveAsync(account, CancellationToken.None);

        var roundTripJob = await jobs.GetAsync(jobId, CancellationToken.None);
        var roundTripStates = await states.GetByJobAsync(jobId, CancellationToken.None);
        var roundTripAccount = await creds.GetAsync("nugs", "Main", CancellationToken.None);
        var roundTripSecret = await vault.GetAsync(secretRef, CancellationToken.None);

        Assert.Equal(job, roundTripJob);
        Assert.Equal(new[] { fileState }, roundTripStates);
        Assert.Equal(account, roundTripAccount);
        Assert.Equal("secret-value", roundTripSecret);
        Assert.True(File.Exists(paths.DatabasePath));
    }
}
