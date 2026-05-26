---
title: C# Multisite Rewrite
status: active
owner: @codex
priority: high
complexity: 3
created: 2026-05-25
updated: 2026-05-25
tags: [feature, csharp, blazor, multisite, migration]
---

# C# Multisite Rewrite

## Summary

The active Nugs-Downloader runtime now lives in the `csharp/` solution. The rewrite preserves Nugs behavior, adds LivePhish support, and keeps provider-specific auth and media extraction isolated behind the shared contract.

## Current Status

- Nugs parity is the baseline implementation for the rewrite.
- LivePhish provider support is wired into the host and covered by tests.
- LivePhish now has opt-in live auth and workflow smoke coverage for the representative path.
- The Blazor web host is the runtime entrypoint for UI, workflow, and provider registration.
- The route/menu/dashboard matrix docs now mirror the live host surface.
- The legacy Go/React runtime has been removed from the repository.

## Release Gates

- `dotnet build csharp/NugsDownloader.sln --no-restore`
- `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --filter LivePhishMediaProviderTests`
- `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --no-restore`
- For UI or host wiring changes, run `dotnet run --project csharp/NugsDownloader.Web`

## Intent

This page is the short feature-level entry for the multisite rewrite. Detailed phase status lives in `docs/roadmaps/csharp-blazor-multisite/`.
