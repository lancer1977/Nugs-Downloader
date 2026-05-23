using Microsoft.Data.Sqlite;
using NugsDownloader.App.Abstractions;

namespace NugsDownloader.Web.Services;

public sealed class SqliteSecretVault : ISecretVault
{
    private readonly SqliteStateStore _store;

    public SqliteSecretVault(SqliteStateStore store)
    {
        _store = store;
    }

    public Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct)
    {
        var secretRef = $"{providerId}:{label}";
        return _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO secret_material (SecretRef, ProviderId, Label, SecretValue)
                VALUES ($SecretRef, $ProviderId, $Label, $SecretValue)
                ON CONFLICT(SecretRef) DO UPDATE SET
                    ProviderId = excluded.ProviderId,
                    Label = excluded.Label,
                    SecretValue = excluded.SecretValue;
                """;
            AddParameter(command, "$SecretRef", secretRef);
            AddParameter(command, "$ProviderId", providerId);
            AddParameter(command, "$Label", label);
            AddParameter(command, "$SecretValue", secret);
            await command.ExecuteNonQueryAsync(ct);
            return secretRef;
        }, ct);
    }

    public Task<string?> GetAsync(string secretRef, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT SecretValue
                FROM secret_material
                WHERE SecretRef = $SecretRef
                LIMIT 1;
                """;
            AddParameter(command, "$SecretRef", secretRef);
            var value = await command.ExecuteScalarAsync(ct);
            return value?.ToString();
        }, ct);

    public Task DeleteAsync(string secretRef, CancellationToken ct) =>
        _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM secret_material WHERE SecretRef = $SecretRef;";
            AddParameter(command, "$SecretRef", secretRef);
            await command.ExecuteNonQueryAsync(ct);
        }, ct);

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
