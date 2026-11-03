using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using NugsDownloader.App.Abstractions;

namespace NugsDownloader.Web.Services;

public sealed class SqliteSecretVault : ISecretVault
{
    private readonly SqliteStateStore _store;
    private readonly IDataProtector? _protector;

    public SqliteSecretVault(SqliteStateStore store, IDataProtectionProvider? dataProtectionProvider = null)
    {
        _store = store;
        _protector = dataProtectionProvider?.CreateProtector("NugsDownloader.SecretVault.v1");
    }

    public Task<string> StoreAsync(string providerId, string label, string secret, CancellationToken ct)
    {
        var secretRef = $"{providerId}:{label}";
        return _store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO secret_material (SecretRef, ProviderId, Label, SecretValue, StorageFormat)
                VALUES ($SecretRef, $ProviderId, $Label, $SecretValue, 'plaintext')
                ON CONFLICT(SecretRef) DO UPDATE SET
                    ProviderId = excluded.ProviderId,
                    Label = excluded.Label,
                    SecretValue = excluded.SecretValue,
                    StorageFormat = excluded.StorageFormat;
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
                SELECT SecretValue, StorageFormat
                FROM secret_material
                WHERE SecretRef = $SecretRef
                LIMIT 1;
                """;
            AddParameter(command, "$SecretRef", secretRef);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            var value = reader.GetString(0);
            var format = reader.GetString(1);
            if (format == "plaintext") return value;
            if (format != "protected" || !value.StartsWith("protected:", StringComparison.Ordinal))
                throw new InvalidOperationException("The saved secret has an unsupported storage format. Sign in again.");
            if (_protector is null) throw new InvalidOperationException("Data protection is required to read a migrated secret.");
            try { return _protector.Unprotect(value["protected:".Length..]); }
            catch (System.Security.Cryptography.CryptographicException)
            {
                throw new InvalidOperationException("The saved secret could not be read. Restore the original data-protection keys or sign in again.");
            }
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
