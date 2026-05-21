using System.Collections;
using NugsDownloader.Domain.ValueObjects;
using NugsDownloader.Infrastructure.Providers.Nugs;
using Xunit;

namespace NugsDownloader.Tests;

public sealed class LiveNugsAuthTests
{
    private const string EnableFlag = "NUGS_LIVE_TESTS";
    private const string DefaultSecretsPath = "/home/lancer1977/.config/secrets/nugs-downloader.env";

    [Fact]
    [Trait("Category", "LiveNugs")]
    public async Task NugsProvider_AuthenticatesWithConfiguredCredentials()
    {
        var secrets = LiveNugsSecrets.Load();
        if (!secrets.Enabled)
        {
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(secrets.Username), "NUGS_USERNAME is required for live auth tests.");
        Assert.False(string.IsNullOrWhiteSpace(secrets.Password), "NUGS_PASSWORD is required for live auth tests.");

        var provider = new NugsMediaProvider();
        var result = await provider.AuthenticateAsync(
            new Credentials(secrets.Username!, secrets.Password!, secrets.Token, "Live integration"),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.SecretRef));
        Assert.StartsWith("Authenticated", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record LiveNugsSecrets(bool Enabled, string? Username, string? Password, string? Token)
    {
        public static LiveNugsSecrets Load()
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    values[key] = value;
                }
            }

            var secretsPath = GetValue(values, "NUGS_SECRETS_FILE") ?? DefaultSecretsPath;
            if (File.Exists(secretsPath))
            {
                foreach (var line in File.ReadAllLines(secretsPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    var separator = trimmed.IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    var name = trimmed[..separator].Trim();
                    if (name.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                    {
                        name = name["export ".Length..].Trim();
                    }

                    var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
                    values[name] = value;
                }
            }

            var enabled = string.Equals(GetValue(values, EnableFlag), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetValue(values, EnableFlag), "true", StringComparison.OrdinalIgnoreCase);

            return new LiveNugsSecrets(
                enabled,
                GetValue(values, "NUGS_USERNAME"),
                GetValue(values, "NUGS_PASSWORD"),
                GetValue(values, "NUGS_TOKEN"));
        }

        private static string? GetValue(IReadOnlyDictionary<string, string?> values, string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}
