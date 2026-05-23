---
title: Backend Layout
status: active
owner: @codex
priority: high
complexity: 2
created: 2026-04-26
updated: 2026-05-22
tags: [feature, backend, layout]
---

# Backend Layout

## Summary

The active backend lives under `csharp/` as an ASP.NET Core solution.

## Current Flow

- `csharp/NugsDownloader.Web` hosts the web app, health checks, and UI.
- `csharp/NugsDownloader.App` coordinates workflows and use cases.
- `csharp/NugsDownloader.Infrastructure` handles providers, downloads, storage, and filesystem helpers.
- `csharp/NugsDownloader.Domain` defines entities, value objects, and contracts.
- `csharp/tests/NugsDownloader.Tests` covers workflow, repository, and UI-facing behavior.

## Checklist

- [x] Backend entrypoint moved to the C# web host
- [x] Shared app logic split into App, Infrastructure, and Domain layers
- [x] Persistence and workflow code live under the C# solution
- [x] Legacy backend removed from the repository
