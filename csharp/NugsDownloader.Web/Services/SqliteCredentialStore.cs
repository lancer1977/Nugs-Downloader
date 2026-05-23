using Microsoft.Data.Sqlite;
using NugsDownloader.App.Abstractions;
using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Web.Services;

public sealed class SqliteCredentialStore : ICredentialStore
{
    private readonly SqliteStateStore _store;

    public SqliteCredentialStore(SqliteStateStore store)
    {
        _store = store;
    }

    public Task<ProviderAccount?> GetAsync(string providerId, string label, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, ProviderId, Label, Username, SecretRef, AuthState, LastVerifiedAt
                FROM provider_accounts
                WHERE ProviderId = $ProviderId AND Label = $Label
                LIMIT 1;
                """;
            AddParameter(command, "$ProviderId", providerId);
            AddParameter(command, "$Label", label);
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapAccount(reader) : null;
        }, ct);

    public Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Id, ProviderId, Label, Username, SecretRef, AuthState, LastVerifiedAt
                FROM provider_accounts
                ORDER BY rowid;
                """;
            await using var reader = await command.ExecuteReaderAsync(ct);
            var accounts = new List<ProviderAccount>();
            while (await reader.ReadAsync(ct))
            {
                accounts.Add(MapAccount(reader));
            }

            return (IReadOnlyList<ProviderAccount>)accounts;
        }, ct);

    public Task SaveAsync(ProviderAccount account, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO provider_accounts (
                    Id, ProviderId, Label, Username, SecretRef, AuthState, LastVerifiedAt
                ) VALUES (
                    $Id, $ProviderId, $Label, $Username, $SecretRef, $AuthState, $LastVerifiedAt
                )
                ON CONFLICT(ProviderId, Label) DO UPDATE SET
                    Id = excluded.Id,
                    Username = excluded.Username,
                    SecretRef = excluded.SecretRef,
                    AuthState = excluded.AuthState,
                    LastVerifiedAt = excluded.LastVerifiedAt;
                """;
            AddParameter(command, "$Id", account.Id.ToString());
            AddParameter(command, "$ProviderId", account.ProviderId);
            AddParameter(command, "$Label", account.Label);
            AddParameter(command, "$Username", account.Username);
            AddParameter(command, "$SecretRef", account.SecretRef);
            AddParameter(command, "$AuthState", (int)account.AuthState);
            AddParameter(command, "$LastVerifiedAt", SqliteStateStore.Encode(account.LastVerifiedAt));
            await command.ExecuteNonQueryAsync(ct);
        }, ct);

    private static ProviderAccount MapAccount(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        (AuthenticationState)reader.GetInt32(5),
        reader.IsDBNull(6) ? null : SqliteStateStore.DecodeDateTimeOffset(reader.GetString(6)));

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
