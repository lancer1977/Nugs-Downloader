using System.Text.Json;
using Microsoft.Data.Sqlite;
using NugsDownloader.Domain.Entities;

namespace NugsDownloader.Web.Services;

internal static class LegacyAccountMigration
{
    public static async Task ImportAsync(SqliteConnection connection, string directory, CancellationToken ct)
    {
        var accountsPath = Path.Combine(directory, "credentials.json");
        var secretsPath = Path.Combine(directory, "secrets.json");
        if (!File.Exists(accountsPath) || !File.Exists(secretsPath)) return;

        List<ProviderAccount> accounts;
        Dictionary<string, string> secrets;
        try
        {
            // Match the legacy serializer: PascalCase properties and numeric enums.
            // Validate explicitly: positional records otherwise silently default missing fields.
            using var accountDocument = JsonDocument.Parse(await File.ReadAllTextAsync(accountsPath, ct));
            using var secretDocument = JsonDocument.Parse(await File.ReadAllTextAsync(secretsPath, ct));
            if (accountDocument.RootElement.ValueKind != JsonValueKind.Array ||
                secretDocument.RootElement.ValueKind != JsonValueKind.Object ||
                HasDuplicateProperties(secretDocument.RootElement)) return;
            secrets = secretDocument.RootElement.Deserialize<Dictionary<string, string>>()!;
            accounts = new List<ProviderAccount>();
            foreach (var row in accountDocument.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || HasDuplicateProperties(row) ||
                    !row.TryGetProperty("Id", out _) || !row.TryGetProperty("Username", out _)) continue;
                ProviderAccount? account;
                try { account = row.Deserialize<ProviderAccount>(); }
                catch (JsonException) { continue; }
                if (account is null || account.Id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(account.ProviderId) || string.IsNullOrWhiteSpace(account.Label) ||
                    account.Username is null || string.IsNullOrWhiteSpace(account.SecretRef) ||
                    !Enum.IsDefined(account.AuthState) || !Enum.IsDefined(account.CredentialKind)) continue;
                accounts.Add(account);
            }
        }
        // Never emit parser/IO exceptions: they can include credential-bearing input.
        catch (JsonException) { return; }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        var duplicateIds = accounts.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var duplicateLabels = accounts.GroupBy(a => (a.ProviderId, a.Label)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var duplicateRefs = accounts.GroupBy(a => a.SecretRef, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

        // Acquire the SQLite write reservation before conflict checks; other store instances
        // cannot insert a competing pair between the check and the inserts.
        using var transaction = connection.BeginTransaction(deferred: false);
        foreach (var account in accounts)
        {
            if (duplicateIds.Contains(account.Id) || duplicateLabels.Contains((account.ProviderId, account.Label)) ||
                duplicateRefs.Contains(account.SecretRef)) continue;
            if (!secrets.TryGetValue(account.SecretRef, out var material) ||
                material is null || !material.StartsWith("protected:", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(material["protected:".Length..])) continue;
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                SELECT EXISTS(SELECT 1 FROM provider_accounts WHERE Id = $id
                    OR (ProviderId = $provider AND Label = $label) OR SecretRef = $ref)
                    OR EXISTS(SELECT 1 FROM secret_material WHERE SecretRef = $ref);
                """;
            command.Parameters.AddWithValue("$id", account.Id.ToString());
            command.Parameters.AddWithValue("$provider", account.ProviderId);
            command.Parameters.AddWithValue("$label", account.Label);
            command.Parameters.AddWithValue("$username", account.Username);
            command.Parameters.AddWithValue("$ref", account.SecretRef);
            command.Parameters.AddWithValue("$auth", (int)account.AuthState);
            command.Parameters.AddWithValue("$verified", (object?)SqliteStateStore.Encode(account.LastVerifiedAt) ?? DBNull.Value);
            command.Parameters.AddWithValue("$kind", (int)account.CredentialKind);
            command.Parameters.AddWithValue("$material", material);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(ct)) != 0) continue;
            command.CommandText = """
                INSERT INTO provider_accounts (Id, ProviderId, Label, Username, SecretRef, AuthState, LastVerifiedAt, CredentialKind)
                VALUES ($id, $provider, $label, $username, $ref, $auth, $verified, $kind);
                INSERT INTO secret_material (SecretRef, ProviderId, Label, SecretValue, StorageFormat)
                VALUES ($ref, $provider, $label, $material, 'protected');
                """;
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        return element.EnumerateObject().Any(property => !names.Add(property.Name));
    }
}
