---
title: Release Notes
status: active
owner: @codex
priority: medium
complexity: 2
created: 2026-05-25
updated: 2026-05-25
tags: [roadmap, release-notes, migration, csharp, blazor, multisite]
---

# Release Notes

This page captures the migration notes for the C# Blazor multisite stack.

## What shipped

- The legacy Go/React implementation has been removed.
- The active application lives under `csharp/` as a .NET solution.
- Nugs parity is the baseline implementation for the rewrite.
- LivePhish now has the first provider slice wired into the host and tested.
- LivePhish includes opt-in live auth and workflow smoke tests behind `LIVEPHISH_LIVE_TESTS`.
- The web host, provider catalog, and roadmap docs all point at the same C# architecture.

## Migration notes

- Build and test the active stack from the repo root with the `csharp/` solution.
- Treat `NugsDownloader.Web` as the host entry point for local runs and deployment wiring.
- Keep site-specific auth and media extraction inside provider implementations.
- Keep job/file state in the shared domain and persistence layers so the UI stays thin.
- Expect private NuGet vulnerability metadata checks to emit `NU1900` warnings when the Azure DevOps feed is unreachable; the build and test flow still completes.

## Operator checklist

- Use the C# solution for new feature work.
- Update the roadmap docs in the same pass as behavior changes.
- Preserve the provider contract when adding future sites.
- Add targeted tests for each new provider slice before closing the roadmap item.
