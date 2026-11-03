using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using NugsDownloader.Domain.Entities;
using NugsDownloader.Web.Options;
using NugsDownloader.Web.Services;
using Xunit;

namespace NugsDownloader.Tests;

public sealed class LegacyAccountMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nugs-migration-{Guid.NewGuid():N}");
    private readonly ProviderAccount _account = new(Guid.NewGuid(), "nugs", "Main", "synthetic-user", "nugs:Main", AuthenticationState.Unknown, null, CredentialKind.Token);
    private const string Material = "protected:synthetic-opaque-material";
    private SqliteStateStore Store() => new(new SqliteStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = _root })));

    private async Task SeedAsync()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "credentials.json"), JsonSerializer.Serialize(new[] { _account }));
        await File.WriteAllTextAsync(Path.Combine(_root, "secrets.json"), JsonSerializer.Serialize(new Dictionary<string, string> { [_account.SecretRef] = Material }));
    }

    [Fact]
    public async Task FirstStoreUseImportsPairWithoutChangingLegacyBytesOrAuthenticationMetadata()
    {
        await SeedAsync();
        var originalAccounts = await File.ReadAllBytesAsync(Path.Combine(_root, "credentials.json"));
        var originalSecrets = await File.ReadAllBytesAsync(Path.Combine(_root, "secrets.json"));
        var store = Store();

        Assert.Equal(_account, Assert.Single(await new SqliteCredentialStore(store).ListAsync(default)));
        Assert.Equal(Material, await store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SecretValue FROM secret_material;";
            return (string?)await command.ExecuteScalarAsync();
        }, default));
        Assert.Equal(originalAccounts, await File.ReadAllBytesAsync(Path.Combine(_root, "credentials.json")));
        Assert.Equal(originalSecrets, await File.ReadAllBytesAsync(Path.Combine(_root, "secrets.json")));
    }

    [Theory]
    [InlineData("protected:synthetic-password")]
    [InlineData("synthetic-password")]
    public async Task RegularSecretWritesRoundTripWithoutInterpretingContent(string secret)
    {
        var vault = new SqliteSecretVault(Store(), new EphemeralDataProtectionProvider());
        var reference = await vault.StoreAsync("nugs", "Main", secret, default);
        Assert.Equal(secret, await vault.GetAsync(reference, default));
    }

    [Theory]
    [InlineData("protected:synthetic-password")]
    [InlineData("synthetic-password")]
    public async Task ExistingSqliteSecretsDefaultToPlaintextDuringSchemaUpgrade(string secret)
    {
        Directory.CreateDirectory(_root);
        await using (var connection = new SqliteConnection($"Data Source={Path.Combine(_root, "nugs-downloader.db")}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE secret_material (SecretRef TEXT PRIMARY KEY, ProviderId TEXT NOT NULL,
                    Label TEXT NOT NULL, SecretValue TEXT NOT NULL);
                INSERT INTO secret_material VALUES ('nugs:Main', 'nugs', 'Main', $value);
                """;
            command.Parameters.AddWithValue("$value", secret);
            await command.ExecuteNonQueryAsync();
        }
        var store = Store();
        Assert.Equal(secret, await new SqliteSecretVault(store).GetAsync(_account.SecretRef, default));
        Assert.Equal("plaintext", await ReadStorageFormatAsync(store));
        Assert.Equal(secret, await new SqliteSecretVault(Store()).GetAsync(_account.SecretRef, default));
    }

    [Theory]
    [InlineData("protected:synthetic-password")]
    [InlineData("synthetic-password")]
    public async Task UpdatingMigratedSecretResetsStorageFormatToPlaintext(string secret)
    {
        await SeedAsync();
        var protection = new EphemeralDataProtectionProvider();
        var paths = new JsonStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = _root }));
        await new JsonSecretVault(paths, protection).StoreAsync("nugs", "Main", "synthetic-original", default);
        var store = Store();
        var vault = new SqliteSecretVault(store, protection);
        Assert.Equal("synthetic-original", await vault.GetAsync(_account.SecretRef, default));
        Assert.Equal("protected", await ReadStorageFormatAsync(store));

        await vault.StoreAsync("nugs", "Main", secret, default);

        Assert.Equal("plaintext", await ReadStorageFormatAsync(store));
        Assert.Equal(secret, await vault.GetAsync(_account.SecretRef, default));
        Assert.Equal(secret, await new SqliteSecretVault(Store()).GetAsync(_account.SecretRef, default));
    }

    private static Task<string?> ReadStorageFormatAsync(SqliteStateStore store) =>
        store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT StorageFormat FROM secret_material WHERE SecretRef = 'nugs:Main';";
            return (string?)await command.ExecuteScalarAsync();
        }, default);

    [Fact]
    public async Task RestartDoesNotOverwriteMigratedPair()
    {
        await SeedAsync();
        var store = Store();
        await new SqliteCredentialStore(store).ListAsync(default);
        var changed = _account with { Username = "new-user", AuthState = AuthenticationState.Invalid };
        await new SqliteCredentialStore(store).SaveAsync(changed, default);
        await new SqliteSecretVault(store).StoreAsync("nugs", "Main", "new-synthetic-material", default);

        var restarted = Store();
        Assert.Equal(changed, Assert.Single(await new SqliteCredentialStore(restarted).ListAsync(default)));
        Assert.Equal("new-synthetic-material", await new SqliteSecretVault(restarted).GetAsync(_account.SecretRef, default));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("label")]
    [InlineData("secret")]
    [InlineData("reference")]
    public async Task ExistingConflictSkipsWholePair(string conflict)
    {
        var store = Store();
        var existing = _account with { Id = Guid.NewGuid(), Label = "Other", SecretRef = "other-ref" };
        if (conflict == "id") existing = existing with { Id = _account.Id };
        if (conflict == "label") existing = existing with { Label = _account.Label };
        if (conflict == "reference") existing = existing with { SecretRef = _account.SecretRef };
        if (conflict == "secret")
            await new SqliteSecretVault(store).StoreAsync("nugs", "Main", "existing-synthetic-material", default);
        else
            await new SqliteCredentialStore(store).SaveAsync(existing, default);
        await SeedAsync();

        var restarted = Store();
        var accounts = await new SqliteCredentialStore(restarted).ListAsync(default);
        if (conflict == "secret") Assert.Empty(accounts);
        else Assert.Equal(existing, Assert.Single(accounts));
        Assert.Equal(conflict == "secret" ? "existing-synthetic-material" : null,
            await new SqliteSecretVault(restarted).GetAsync(_account.SecretRef, default));
    }

    [Theory]
    [InlineData("malformed-accounts")]
    [InlineData("malformed-secrets")]
    [InlineData("null-account")]
    [InlineData("missing-id")]
    [InlineData("missing-username")]
    [InlineData("empty-provider")]
    [InlineData("empty-label")]
    [InlineData("empty-ref")]
    [InlineData("unknown-auth")]
    [InlineData("unknown-kind")]
    [InlineData("bad-date")]
    [InlineData("null-secret")]
    [InlineData("empty-protected-secret")]
    [InlineData("plaintext-secret")]
    [InlineData("duplicate-property")]
    [InlineData("duplicate-secret-key")]
    public async Task InvalidInputImportsNothingAndLeavesFilesUnchanged(string invalid)
    {
        await SeedAsync();
        var accountPath = Path.Combine(_root, "credentials.json");
        var secretPath = Path.Combine(_root, "secrets.json");
        var accountJson = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(accountPath))!;
        var row = accountJson[0]!;
        switch (invalid)
        {
            case "missing-id": row.AsObject().Remove("Id"); break;
            case "missing-username": row.AsObject().Remove("Username"); break;
            case "empty-provider": row["ProviderId"] = " "; break;
            case "empty-label": row["Label"] = ""; break;
            case "empty-ref": row["SecretRef"] = ""; break;
            case "unknown-auth": row["AuthState"] = 99; break;
            case "unknown-kind": row["CredentialKind"] = 99; break;
            case "bad-date": row["LastVerifiedAt"] = "not-a-date"; break;
        }
        var accountsText = invalid switch
        {
            "malformed-accounts" => "[broken",
            "null-account" => "[null]",
            "duplicate-property" => accountJson.ToJsonString().Replace("\"ProviderId\":", "\"ProviderId\":\"other\",\"ProviderId\":"),
            _ => accountJson.ToJsonString()
        };
        var secretsText = invalid switch
        {
            "malformed-secrets" => "[1]",
            "null-secret" => "{\"nugs:Main\":null}",
            "empty-protected-secret" => "{\"nugs:Main\":\"protected:\"}",
            "plaintext-secret" => "{\"nugs:Main\":\"synthetic-plaintext\"}",
            "duplicate-secret-key" => "{\"nugs:Main\":\"protected:one\",\"nugs:Main\":\"protected:two\"}",
            _ => await File.ReadAllTextAsync(secretPath)
        };
        await File.WriteAllTextAsync(accountPath, accountsText);
        await File.WriteAllTextAsync(secretPath, secretsText);
        var store = Store();
        Assert.Empty(await new SqliteCredentialStore(store).ListAsync(default));
        Assert.Null(await new SqliteSecretVault(store).GetAsync(_account.SecretRef, default));
        Assert.Equal(accountsText, await File.ReadAllTextAsync(accountPath));
        Assert.Equal(secretsText, await File.ReadAllTextAsync(secretPath));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("label")]
    [InlineData("reference")]
    public async Task AmbiguousLegacyAccountsAreAllSkipped(string duplicate)
    {
        await SeedAsync();
        var other = _account with { Id = Guid.NewGuid(), Label = "Other", SecretRef = "nugs:Other" };
        if (duplicate == "id") other = other with { Id = _account.Id };
        if (duplicate == "label") other = other with { Label = _account.Label };
        if (duplicate == "reference") other = other with { SecretRef = _account.SecretRef };
        await File.WriteAllTextAsync(Path.Combine(_root, "credentials.json"), JsonSerializer.Serialize(new[] { _account, other }));
        await File.WriteAllTextAsync(Path.Combine(_root, "secrets.json"), JsonSerializer.Serialize(
            new Dictionary<string, string> { [_account.SecretRef] = Material, [other.SecretRef] = Material }));
        var store = Store();
        Assert.Empty(await new SqliteCredentialStore(store).ListAsync(default));
        Assert.Null(await new SqliteSecretVault(store).GetAsync(_account.SecretRef, default));
        Assert.Null(await new SqliteSecretVault(store).GetAsync(other.SecretRef, default));
    }

    [Fact]
    public async Task MigratedProtectedMaterialCanBeReadUsingLegacyDataProtectionPurpose()
    {
        await SeedAsync();
        var protection = new EphemeralDataProtectionProvider();
        var paths = new JsonStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = _root }));
        await new JsonSecretVault(paths, protection).StoreAsync("nugs", "Main", "synthetic-cleartext", default);
        var serialized = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(paths.Secrets))!;
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(protection);
        services.AddSingleton(Store());
        services.AddSingleton<SqliteSecretVault>();
        using var provider = services.BuildServiceProvider();
        var vault = provider.GetRequiredService<SqliteSecretVault>();
        Assert.Equal("synthetic-cleartext", await vault.GetAsync(_account.SecretRef, default));
        Assert.Equal(serialized[_account.SecretRef], await provider.GetRequiredService<SqliteStateStore>().ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SecretValue FROM secret_material;";
            return (string?)await command.ExecuteScalarAsync();
        }, default));
    }

    [Fact]
    public async Task ActualHostImportsBeforeAnyRequestOrBackgroundWorker()
    {
        await SeedAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("NugsDownloader:StateDirectory", _root);
            builder.UseSetting("NugsDownloader:DownloadDirectory", Path.Combine(_root, "downloads"));
            // No worker is allowed to incidentally initialize storage for this assertion.
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
        });
        _ = factory.Services;
        Assert.True(File.Exists(Path.Combine(_root, "nugs-downloader.db")), "Startup must initialize and migrate before serving requests.");
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(_root, "nugs-downloader.db")};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM provider_accounts;";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/queue");
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(_account, Assert.Single(await factory.Services.GetRequiredService<NugsDownloader.App.Abstractions.ICredentialStore>().ListAsync(default)));
    }

    [Fact]
    public async Task DatabaseFailureRollsBackEntireBatchAndCanRetryWithoutExposingValues()
    {
        var initial = Store();
        await initial.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER fail_migration BEFORE INSERT ON secret_material
                WHEN NEW.Label = 'Other'
                BEGIN SELECT RAISE(ABORT, 'synthetic-sensitive-database-error'); END;
                """;
            await command.ExecuteNonQueryAsync();
        }, default);
        await SeedAsync();
        var other = _account with { Id = Guid.NewGuid(), Label = "Other", SecretRef = "nugs:Other" };
        await File.WriteAllTextAsync(Path.Combine(_root, "credentials.json"), JsonSerializer.Serialize(new[] { _account, other }));
        await File.WriteAllTextAsync(Path.Combine(_root, "secrets.json"), JsonSerializer.Serialize(
            new Dictionary<string, string> { [_account.SecretRef] = Material, [other.SecretRef] = Material }));
        var store = Store();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new SqliteCredentialStore(store).ListAsync(default));
        Assert.DoesNotContain("synthetic-sensitive", error.ToString());
        await initial.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT COUNT(*) FROM provider_accounts) + (SELECT COUNT(*) FROM secret_material);";
            Assert.Equal(0L, await command.ExecuteScalarAsync());
            command.CommandText = "DROP TRIGGER fail_migration;";
            await command.ExecuteNonQueryAsync();
        }, default);
        Assert.Equal(2, (await new SqliteCredentialStore(store).ListAsync(default)).Count);
    }

    [Theory]
    [InlineData("credentials.json")]
    [InlineData("secrets.json")]
    [InlineData("both")]
    public async Task MissingFilesAreANoOp(string missing)
    {
        await SeedAsync();
        if (missing is "credentials.json" or "both") File.Delete(Path.Combine(_root, "credentials.json"));
        if (missing is "secrets.json" or "both") File.Delete(Path.Combine(_root, "secrets.json"));
        var store = Store();
        Assert.Empty(await new SqliteCredentialStore(store).ListAsync(default));
        Assert.Null(await new SqliteSecretVault(store).GetAsync(_account.SecretRef, default));
    }

    [Theory]
    [InlineData("null", "{}")]
    [InlineData("{}", "{}")]
    [InlineData("[]", "null")]
    [InlineData("[]", "[]")]
    public async Task WrongRootStructuresAreANoOp(string accounts, string secrets)
    {
        await SeedAsync();
        await File.WriteAllTextAsync(Path.Combine(_root, "credentials.json"), accounts);
        await File.WriteAllTextAsync(Path.Combine(_root, "secrets.json"), secrets);
        Assert.Empty(await new SqliteCredentialStore(Store()).ListAsync(default));
    }

    [Fact]
    public async Task OnlyCompletePairsImportAndUnreferencedSecretsAreNotCopied()
    {
        await SeedAsync();
        var unmatched = _account with { Id = Guid.NewGuid(), Label = "Missing", SecretRef = "missing" };
        await File.WriteAllTextAsync(Path.Combine(_root, "credentials.json"), JsonSerializer.Serialize(new[] { _account, unmatched }));
        await File.WriteAllTextAsync(Path.Combine(_root, "secrets.json"), JsonSerializer.Serialize(
            new Dictionary<string, string> { [_account.SecretRef] = Material, ["unreferenced"] = Material }));
        var store = Store();
        Assert.Equal(_account, Assert.Single(await new SqliteCredentialStore(store).ListAsync(default)));
        Assert.Null(await new SqliteSecretVault(store).GetAsync("unreferenced", default));
        Assert.Null(await new SqliteSecretVault(store).GetAsync("missing", default));
    }

    [Theory]
    [InlineData(AuthenticationState.Unknown)]
    [InlineData(AuthenticationState.Valid)]
    [InlineData(AuthenticationState.Expired)]
    [InlineData(AuthenticationState.Invalid)]
    public async Task LegacyMetadataIsPreservedWithOptionalCredentialKindDefault(AuthenticationState state)
    {
        await SeedAsync();
        var verified = DateTimeOffset.Parse("2026-01-02T03:04:05+02:00");
        var expected = _account with { AuthState = state, LastVerifiedAt = verified, CredentialKind = CredentialKind.Password };
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new[] { expected }))!;
        json[0]!.AsObject().Remove("CredentialKind");
        await File.WriteAllTextAsync(Path.Combine(_root, "credentials.json"), json.ToJsonString());
        Assert.Equal(expected, Assert.Single(await new SqliteCredentialStore(Store()).ListAsync(default)));
    }

    [Fact]
    public async Task IndependentStoreInstancesDoNotDuplicatePairs()
    {
        // Create schema before racing independent migration transactions.
        await Store().ExecuteAsync(_ => Task.CompletedTask, default);
        await SeedAsync();
        var stores = new[] { Store(), Store() };
        var results = await Task.WhenAll(stores.Select(s => Task.Run(() => new SqliteCredentialStore(s).ListAsync(default))));
        Assert.All(results, accounts => Assert.Equal(_account, Assert.Single(accounts)));
    }

    [Fact]
    public async Task MissingProtectionKeysFailSafelyWithoutChangingStoredMaterial()
    {
        await SeedAsync();
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton(Store());
        services.AddSingleton<SqliteSecretVault>();
        using var provider = services.BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRequiredService<SqliteSecretVault>().GetAsync(_account.SecretRef, default));
        Assert.DoesNotContain(Material, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task ValidCiphertextFromDifferentKeyProviderFailsWithoutFallbackOrMutation()
    {
        await SeedAsync();
        var originalProtection = new EphemeralDataProtectionProvider();
        var paths = new JsonStorePaths(Options.Create(new NugsDownloaderStorageOptions { StateDirectory = _root }));
        await new JsonSecretVault(paths, originalProtection).StoreAsync("nugs", "Main", "synthetic-cleartext", default);
        var material = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(paths.Secrets))![_account.SecretRef];
        var store = Store();
        // Prove this is authentic ciphertext, not just malformed protected-looking text.
        Assert.Equal("synthetic-cleartext", await new SqliteSecretVault(store, originalProtection).GetAsync(_account.SecretRef, default));

        var wrongKeys = new SqliteSecretVault(store, new EphemeralDataProtectionProvider());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => wrongKeys.GetAsync(_account.SecretRef, default));
        Assert.DoesNotContain(material, error.ToString());
        Assert.DoesNotContain("synthetic-cleartext", error.ToString());
        Assert.Null(error.InnerException);
        Assert.Equal("protected", await ReadStorageFormatAsync(store));
        Assert.Equal(material, await store.ExecuteAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SecretValue FROM secret_material;";
            return (string?)await command.ExecuteScalarAsync();
        }, default));
        Assert.Equal("synthetic-cleartext", await new SqliteSecretVault(Store(), originalProtection).GetAsync(_account.SecretRef, default));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
