using Microsoft.Data.Sqlite;
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
            new DownloadPreferences("flac", "1080p", false, false, false, "Downloads", true, true),
            "test-worker",
            DateTimeOffset.Parse("2026-05-22T10:00:05.0000000+00:00"),
            DateTimeOffset.Parse("2026-05-22T10:05:05.0000000+00:00"),
            2,
            DateTimeOffset.Parse("2026-05-22T10:06:05.0000000+00:00"));

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
            DateTimeOffset.Parse("2026-05-22T10:02:00.0000000+00:00"),
            CredentialKind.Token);

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

    [Fact]
    public async Task SqliteCredentialStore_UpgradesLegacyAccountsAndPreservesTokenCredentialKind()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = Options.Create(new NugsDownloaderStorageOptions { StateDirectory = stateDirectory });
        var paths = new SqliteStorePaths(options);
        Directory.CreateDirectory(stateDirectory);

        await using (var connection = new SqliteConnection($"Data Source={paths.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE provider_accounts (
                    Id TEXT PRIMARY KEY,
                    ProviderId TEXT NOT NULL,
                    Label TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    SecretRef TEXT NOT NULL,
                    AuthState INTEGER NOT NULL,
                    LastVerifiedAt TEXT NULL,
                    UNIQUE(ProviderId, Label)
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteStateStore(paths);
        var credentials = new SqliteCredentialStore(store);
        var account = new ProviderAccount(
            Guid.NewGuid(),
            "nugs",
            "Token",
            string.Empty,
            "nugs:Token",
            AuthenticationState.Valid,
            DateTimeOffset.UtcNow,
            CredentialKind.Token);

        await credentials.SaveAsync(account, CancellationToken.None);

        Assert.Equal(account, await credentials.GetAsync("nugs", "Token", CancellationToken.None));
    }

    [Fact]
    public async Task SqliteJobRepository_AcquiresLeaseWithSingleConditionalWriter()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new SqliteStateStore(new SqliteStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = stateDirectory })));
        var first = new SqliteJobRepository(store);
        var second = new SqliteJobRepository(store);
        var job = new DownloadJob(
            Guid.NewGuid(),
            "nugs",
            new Uri("https://example.com/release/1"),
            "Demo",
            DownloadJobStatus.Ready,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            "Downloads");
        await first.SaveAsync(job, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;

        var results = await Task.WhenAll(
            first.TryAcquireLeaseAsync(job.Id, "worker-a", now, now.AddMinutes(5), CancellationToken.None),
            second.TryAcquireLeaseAsync(job.Id, "worker-b", now, now.AddMinutes(5), CancellationToken.None));

        Assert.Single(results, acquired => acquired);
        var leased = await first.GetAsync(job.Id, CancellationToken.None);
        Assert.Contains(leased!.LeaseOwnerId, new[] { "worker-a", "worker-b" });
    }

    [Fact]
    public async Task SqliteJobRepository_RejectsDuplicateAcquisitionBySameWorkerAndRenewsExplicitly()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var jobs = new SqliteJobRepository(new SqliteStateStore(new SqliteStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = stateDirectory }))));
        var job = new DownloadJob(Guid.NewGuid(), "nugs", new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Ready, DateTimeOffset.UtcNow, null, null, null, "Downloads");
        await jobs.SaveAsync(job, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;

        Assert.True(await jobs.TryAcquireLeaseAsync(job.Id, "worker-a", now, now.AddMinutes(1), CancellationToken.None));
        Assert.False(await jobs.TryAcquireLeaseAsync(job.Id, "worker-a", now.AddSeconds(1), now.AddMinutes(2), CancellationToken.None));
        Assert.True(await jobs.RenewLeaseAsync(job.Id, "worker-a", now.AddMinutes(3), CancellationToken.None));
        Assert.Equal(now.AddMinutes(3), (await jobs.GetAsync(job.Id, CancellationToken.None))!.LeaseExpiresAt);
    }

    [Fact]
    public async Task SqliteJobRepository_RejectsLeaseAcquisitionBeforeRetryBackoffExpires()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var jobs = new SqliteJobRepository(new SqliteStateStore(new SqliteStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = stateDirectory }))));
        var now = DateTimeOffset.UtcNow;
        var job = new DownloadJob(Guid.NewGuid(), "nugs", new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Ready, now, null, null, null, "Downloads", NextAttemptAt: now.AddMinutes(5));
        await jobs.SaveAsync(job, CancellationToken.None);

        Assert.False(await jobs.TryAcquireLeaseAsync(job.Id, "worker-a", now, now.AddMinutes(1), CancellationToken.None));
        Assert.True(await jobs.TryAcquireLeaseAsync(job.Id, "worker-a", now.AddMinutes(6), now.AddMinutes(7), CancellationToken.None));
    }

    [Fact]
    public async Task SqliteJobRepository_FencesWritesFromWorkerThatLostLease()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var jobs = new SqliteJobRepository(new SqliteStateStore(new SqliteStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = stateDirectory }))));
        var now = DateTimeOffset.UtcNow;
        var job = new DownloadJob(Guid.NewGuid(), "nugs", new Uri("https://example.com/release/1"), "Demo", DownloadJobStatus.Running, now, now, null, null, "Downloads", LeaseOwnerId: "worker-b", LeaseAcquiredAt: now, LeaseExpiresAt: now.AddMinutes(5));
        await jobs.SaveAsync(job, CancellationToken.None);

        var saved = await jobs.SaveIfLeaseOwnedAsync(job with { Status = DownloadJobStatus.Cancelled }, "worker-a", CancellationToken.None);

        Assert.False(saved);
        var current = await jobs.GetAsync(job.Id, CancellationToken.None);
        Assert.Equal("worker-b", current!.LeaseOwnerId);
        Assert.Equal(DownloadJobStatus.Running, current.Status);
    }
}
