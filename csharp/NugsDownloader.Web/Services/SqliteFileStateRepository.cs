using Microsoft.Data.Sqlite;
using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Web.Services;

public sealed class SqliteFileStateRepository : IFileStateRepository
{
    private readonly SqliteStateStore _store;

    public SqliteFileStateRepository(SqliteStateStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<FileState>> GetByJobAsync(Guid jobId, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, JobId, FilePath, Kind, FileStatus, ExpectedSize, ActualSize, Checksum, LastVerifiedAt
                FROM file_states
                WHERE JobId = $JobId
                ORDER BY rowid;
                """;
            AddParameter(command, "$JobId", jobId.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct);
            var states = new List<FileState>();
            while (await reader.ReadAsync(ct))
            {
                states.Add(MapState(reader));
            }

            return (IReadOnlyList<FileState>)states;
        }, ct);

    public Task SaveAsync(FileState state, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO file_states (
                    Id, JobId, FilePath, Kind, FileStatus, ExpectedSize, ActualSize, Checksum, LastVerifiedAt
                ) VALUES (
                    $Id, $JobId, $FilePath, $Kind, $FileStatus, $ExpectedSize, $ActualSize, $Checksum, $LastVerifiedAt
                )
                ON CONFLICT(Id) DO UPDATE SET
                    JobId = excluded.JobId,
                    FilePath = excluded.FilePath,
                    Kind = excluded.Kind,
                    FileStatus = excluded.FileStatus,
                    ExpectedSize = excluded.ExpectedSize,
                    ActualSize = excluded.ActualSize,
                    Checksum = excluded.Checksum,
                    LastVerifiedAt = excluded.LastVerifiedAt;
                """;
            AddParameter(command, "$Id", state.Id.ToString());
            AddParameter(command, "$JobId", state.JobId.ToString());
            AddParameter(command, "$FilePath", state.FilePath);
            AddParameter(command, "$Kind", (int)state.Kind);
            AddParameter(command, "$FileStatus", (int)state.Status);
            AddParameter(command, "$ExpectedSize", state.ExpectedSize);
            AddParameter(command, "$ActualSize", state.ActualSize);
            AddParameter(command, "$Checksum", state.Checksum);
            AddParameter(command, "$LastVerifiedAt", SqliteStateStore.Encode(state.LastVerifiedAt));
            await command.ExecuteNonQueryAsync(ct);
        }, ct);

    private static FileState MapState(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        Guid.Parse(reader.GetString(1)),
        reader.GetString(2),
        (FileKind)reader.GetInt32(3),
        (FileStatus)reader.GetInt32(4),
        reader.GetInt64(5),
        reader.GetInt64(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : SqliteStateStore.DecodeDateTimeOffset(reader.GetString(8)));

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
