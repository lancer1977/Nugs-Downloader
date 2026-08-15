---
name: nugs-downloader
description: Operate the LAN-only Nugs Downloader application or its explicitly installed command-line companion. Use when a user asks to submit, inspect, or troubleshoot a nugs.net download without exposing credentials or making the service public.
---

# Nugs Downloader

`lancer1977/Nugs-Downloader` owns the active Nugs product. It is a .NET 10
Blazor web application deployed as the internal `nugs.polyhydragames.com`
service. The repository also documents a legacy/companion `nugs-downloader`
executable; never assume that executable is installed or that it is the web
application.

## Operating boundary

- Keep the service LAN-only. Do not add public DNS, remove the Traefik
  allowlist, or expose the application to the internet.
- Credentials belong in the configured secret store or application secret
  contract. Never put Nugs credentials, tokens, cookies, or download URLs with
  embedded secrets in prompts, logs, commits, or skill text.
- Treat downloaded media and the configured download directory as private
  user data. Do not copy it into a repository or a temporary artifact bundle.
- Do not run a live download or mutate the service unless the user explicitly
  asks for that operation.

## Choose the supported surface

For normal user downloads, use the internal web application and its documented
job workflow. Verify the service health endpoint first when troubleshooting:

```text
https://nugs.polyhydragames.com/health/ready
```

For local development, run the application from the repository checkout:

```bash
dotnet run --project csharp/NugsDownloader.Web
```

If the user explicitly requests the companion CLI, first verify the executable
and read its current help output; do not assume `~/bin/nugs-downloader` or a
specific argument spelling:

```bash
command -v nugs-downloader
nugs-downloader --help
```

Only pass a user-provided release URL or ID after the command contract has been
verified. Use a temporary output directory for tests and preserve the user's
configured download root for real operations.

## Validation

Use repository-local build/tests for code changes:

```bash
dotnet build csharp/NugsDownloader.sln
dotnet test csharp/NugsDownloader.sln
```

Live download tests require the repository's explicit live-test opt-in and
secret contract. Do not enable them as a routine skill action.
