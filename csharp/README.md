# C# Rewrite

This directory contains the active C# implementation of Nugs-Downloader.

## Structure

- `NugsDownloader.Domain` - entities, value objects, and contracts
- `NugsDownloader.App` - workflow orchestration and use cases
- `NugsDownloader.Infrastructure` - providers, downloads, persistence, and filesystem helpers
- `NugsDownloader.Web` - Blazor web host, UI, health checks, and runtime composition
- `tests/NugsDownloader.Tests` - workflow, repository, and UI-facing tests

Run the solution from the repo root with:

```bash
dotnet test csharp/NugsDownloader.sln
```
