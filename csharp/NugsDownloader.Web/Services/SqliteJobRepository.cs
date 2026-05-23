using System.Text.Json;
using Microsoft.Data.Sqlite;
using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.Web.Services;

public sealed class SqliteJobRepository : IJobRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly SqliteStateStore _store;

    public SqliteJobRepository(SqliteStateStore store)
    {
        _store = store;
    }

    public Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, ProviderId, SourceUrl, DisplayName, JobStatus, RequestedAt, StartedAt, CompletedAt, ErrorMessage, OutputPath, CredentialLabel, CredentialUsername, PreferencesJson
                FROM jobs
                WHERE Id = $id
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return MapJob(reader);
        }, ct);

    public Task<IReadOnlyList<DownloadJob>> ListAsync(CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, ProviderId, SourceUrl, DisplayName, JobStatus, RequestedAt, StartedAt, CompletedAt, ErrorMessage, OutputPath, CredentialLabel, CredentialUsername, PreferencesJson
                FROM jobs
                ORDER BY RequestedAt, rowid;
                """;
            await using var reader = await command.ExecuteReaderAsync(ct);
            var jobs = new List<DownloadJob>();
            while (await reader.ReadAsync(ct))
            {
                jobs.Add(MapJob(reader));
            }

            return (IReadOnlyList<DownloadJob>)jobs;
        }, ct);

    public Task SaveAsync(DownloadJob job, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO jobs (
                    Id, ProviderId, SourceUrl, DisplayName, JobStatus, RequestedAt, StartedAt, CompletedAt, ErrorMessage, OutputPath, CredentialLabel, CredentialUsername, PreferencesJson
                ) VALUES (
                    $Id, $ProviderId, $SourceUrl, $DisplayName, $JobStatus, $RequestedAt, $StartedAt, $CompletedAt, $ErrorMessage, $OutputPath, $CredentialLabel, $CredentialUsername, $PreferencesJson
                )
                ON CONFLICT(Id) DO UPDATE SET
                    ProviderId = excluded.ProviderId,
                    SourceUrl = excluded.SourceUrl,
                    DisplayName = excluded.DisplayName,
                    JobStatus = excluded.JobStatus,
                    RequestedAt = excluded.RequestedAt,
                    StartedAt = excluded.StartedAt,
                    CompletedAt = excluded.CompletedAt,
                    ErrorMessage = excluded.ErrorMessage,
                    OutputPath = excluded.OutputPath,
                    CredentialLabel = excluded.CredentialLabel,
                    CredentialUsername = excluded.CredentialUsername,
                    PreferencesJson = excluded.PreferencesJson;
                """;
            AddParameter(command, "$Id", job.Id.ToString());
            AddParameter(command, "$ProviderId", job.ProviderId);
            AddParameter(command, "$SourceUrl", job.SourceUrl.ToString());
            AddParameter(command, "$DisplayName", job.DisplayName);
            AddParameter(command, "$JobStatus", (int)job.Status);
            AddParameter(command, "$RequestedAt", SqliteStateStore.Encode(job.RequestedAt));
            AddParameter(command, "$StartedAt", SqliteStateStore.Encode(job.StartedAt));
            AddParameter(command, "$CompletedAt", SqliteStateStore.Encode(job.CompletedAt));
            AddParameter(command, "$ErrorMessage", job.ErrorMessage);
            AddParameter(command, "$OutputPath", job.OutputPath);
            AddParameter(command, "$CredentialLabel", job.CredentialLabel);
            AddParameter(command, "$CredentialUsername", job.CredentialUsername);
            AddParameter(command, "$PreferencesJson", job.Preferences is null ? null : JsonSerializer.Serialize(job.Preferences, JsonOptions));
            await command.ExecuteNonQueryAsync(ct);
        }, ct);

    private static DownloadJob MapJob(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        new Uri(reader.GetString(2)),
        reader.GetString(3),
        (DownloadJobStatus)reader.GetInt32(4),
        SqliteStateStore.DecodeDateTimeOffset(reader.GetString(5)) ?? DateTimeOffset.MinValue,
        reader.IsDBNull(6) ? null : SqliteStateStore.DecodeDateTimeOffset(reader.GetString(6)),
        reader.IsDBNull(7) ? null : SqliteStateStore.DecodeDateTimeOffset(reader.GetString(7)),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : JsonSerializer.Deserialize<DownloadPreferences>(reader.GetString(12), JsonOptions));

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
