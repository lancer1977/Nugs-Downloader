using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NugsDownloader.Web.Services;

public sealed class SqliteStateStore
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public SqliteStateStore(SqliteStorePaths paths)
    {
        _databasePath = paths.DatabasePath;
    }

    public async Task<T> ExecuteAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await EnsureCreatedAsync(ct);
            await using var connection = new SqliteConnection(BuildConnectionString());
            await connection.OpenAsync(ct);
            return await action(connection);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ExecuteAsync(Func<SqliteConnection, Task> action, CancellationToken ct)
    {
        await ExecuteAsync(async connection =>
        {
            await action(connection);
            return true;
        }, ct);
    }

    private async Task EnsureCreatedAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection(BuildConnectionString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS jobs (
                Id TEXT PRIMARY KEY,
                ProviderId TEXT NOT NULL,
                SourceUrl TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                JobStatus INTEGER NOT NULL,
                RequestedAt TEXT NOT NULL,
                StartedAt TEXT NULL,
                CompletedAt TEXT NULL,
                ErrorMessage TEXT NULL,
                OutputPath TEXT NOT NULL,
                CredentialLabel TEXT NULL,
                CredentialUsername TEXT NULL,
                PreferencesJson TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS file_states (
                Id TEXT PRIMARY KEY,
                JobId TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                Kind INTEGER NOT NULL,
                FileStatus INTEGER NOT NULL,
                ExpectedSize INTEGER NOT NULL,
                ActualSize INTEGER NOT NULL,
                Checksum TEXT NULL,
                LastVerifiedAt TEXT NULL,
                FOREIGN KEY(JobId) REFERENCES jobs(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS provider_accounts (
                Id TEXT PRIMARY KEY,
                ProviderId TEXT NOT NULL,
                Label TEXT NOT NULL,
                Username TEXT NOT NULL,
                SecretRef TEXT NOT NULL,
                AuthState INTEGER NOT NULL,
                LastVerifiedAt TEXT NULL,
                UNIQUE(ProviderId, Label)
            );

            CREATE TABLE IF NOT EXISTS secret_material (
                SecretRef TEXT PRIMARY KEY,
                ProviderId TEXT NOT NULL,
                Label TEXT NOT NULL,
                SecretValue TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct);
        _initialized = true;
    }

    private string BuildConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = _databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    public static string? Encode(DateTimeOffset? value) => value?.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset? DecodeDateTimeOffset(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
