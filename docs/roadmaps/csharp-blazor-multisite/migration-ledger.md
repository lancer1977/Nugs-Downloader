---
title: Migration Ledger
status: active
owner: @codex
priority: high
complexity: 4
created: 2026-04-26
updated: 2026-05-22
tags: [roadmap, migration, ledger, csharp]
---

# Migration Ledger

## Current Architecture Map

### Entry Points and UI

- `NugsDownloader.Web` hosts the app, routing, health checks, and Blazor UI.
- `NugsDownloader.Web/Components` contains the interactive pages and layout.

### Configuration and Startup

- `NugsDownloader.Web` binds configuration and wires the host together.
- `NugsDownloader.App.Abstractions` defines application-facing ports.

### Domain and Models

- `NugsDownloader.Domain` contains the shared entities, value objects, providers, and state models.

### API and Auth

- `NugsDownloader.Infrastructure` contains the provider clients and auth helpers.

### Download Orchestration

- `NugsDownloader.Infrastructure.Downloads` handles fetch and resume behavior.
- `NugsDownloader.App.UseCases` coordinates workflows and job execution.

### Web/API Surface

- `NugsDownloader.Web` exposes the user-facing pages and HTTP endpoints.

## Replacement Principles

- Keep pure data types in `Domain` first.
- Keep workflow logic in `App`.
- Keep IO, crypto, and HTTP in `Infrastructure`.
- Keep the Web project thin and focused on presentation.

## Port Order

1. Domain models
2. Provider contracts
3. Job/state persistence
4. Authentication and session setup
5. Nugs download pipeline
6. LivePhish provider

