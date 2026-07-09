using NugsDownloader.Domain.ValueObjects;

namespace NugsDownloader.App.UseCases;

internal static class DownloadMessageRedactor
{
    private const string Redacted = "[REDACTED]";

    public static string Redact(string? message, Credentials? credentials = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var redacted = message;
        foreach (var secret in EnumerateSecrets(credentials))
        {
            redacted = redacted.Replace(secret, Redacted, StringComparison.Ordinal);
        }

        redacted = RedactQueryValue(redacted, "token");
        redacted = RedactQueryValue(redacted, "access_token");
        redacted = RedactQueryValue(redacted, "password");
        redacted = RedactQueryValue(redacted, "secret");
        return redacted;
    }

    private static IEnumerable<string> EnumerateSecrets(Credentials? credentials)
    {
        if (credentials is null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(credentials.Password))
        {
            yield return credentials.Password;
        }

        if (!string.IsNullOrWhiteSpace(credentials.Token))
        {
            yield return credentials.Token;
        }
    }

    private static string RedactQueryValue(string value, string key)
    {
        var markers = new[] { $"{key}=", $"{Uri.EscapeDataString(key)}=" };
        foreach (var marker in markers)
        {
            var start = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            while (start >= 0)
            {
                var valueStart = start + marker.Length;
                var valueEnd = value.IndexOfAny(new[] { '&', ' ', '\r', '\n', '\t', '"', '\'' }, valueStart);
                if (valueEnd < 0)
                {
                    valueEnd = value.Length;
                }

                value = value[..valueStart] + Redacted + value[valueEnd..];
                start = value.IndexOf(marker, valueStart + Redacted.Length, StringComparison.OrdinalIgnoreCase);
            }
        }

        return value;
    }
}
