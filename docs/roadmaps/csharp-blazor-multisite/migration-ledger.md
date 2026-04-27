---
title: Migration Ledger
status: active
owner: @codex
priority: high
complexity: 4
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, migration, ledger, csharp]
---

# Migration Ledger

## Go To C# Mapping

### Entry Points and UI

- `src/main.go` -> `NugsDownloader.Web` host startup and routing
- `src/ui_embedded.go` -> no direct equivalent; UI is native Blazor
- `ui/` -> `NugsDownloader.Web` components/pages

### Configuration and Startup

- `src/pkg/config` -> `NugsDownloader.App.Abstractions` + `NugsDownloader.Web` configuration binding
- `src/pkg/fsutil` -> `NugsDownloader.Infrastructure.Filesystem`
- `src/pkg/logger` -> `NugsDownloader.Web` logging + shared logging setup

### Domain and Models

- `src/pkg/models` -> `NugsDownloader.Domain`
- `src/pkg/models/progress.go` -> `NugsDownloader.Domain.State`
- `src/pkg/models/types.go` -> `NugsDownloader.Domain.Entities`, `ValueObjects`, `Providers`, and `State`

### API and Auth

- `src/pkg/api` -> `NugsDownloader.Infrastructure.Auth` and site-specific provider clients

### Download Orchestration

- `src/pkg/downloader` -> `NugsDownloader.Infrastructure.Downloads`
- `src/pkg/processor` -> `NugsDownloader.App.UseCases`

### Web/API Surface

- `src/pkg/server` -> `NugsDownloader.Web` endpoints and Blazor pages

## Replacement Principles

- Move pure data types into `Domain` first.
- Move workflow logic into `App`.
- Keep IO, crypto, and HTTP in `Infrastructure`.
- Keep the Web project thin and focused on presentation.

## Port Order

1. Domain models
2. Provider contracts
3. Job/state persistence
4. Authentication and session setup
5. Nugs download pipeline
6. LivePhish provider

