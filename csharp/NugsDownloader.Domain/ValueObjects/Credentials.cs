namespace NugsDownloader.Domain.ValueObjects;

public sealed record Credentials(
    string? Username,
    string? Password,
    string? Token,
    string? Label = null);

