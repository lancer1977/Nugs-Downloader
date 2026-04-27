---
title: Nugs Parity Checklist
status: active
owner: @codex
priority: high
complexity: 4
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, checklist, nugs, parity, csharp]
---

# Nugs Parity Checklist

## Purpose

This checklist captures the remaining work needed to make the C# rewrite cover the current Go-era Nugs behavior closely enough to retire the legacy path.

## Scope

- Keep Nugs as the first migration target.
- Focus on behavior parity before expanding to LivePhish.
- Treat the checklist as the operational source of truth for phase 3.

## Discovery

- [x] Identify the staged C# solution layout
- [x] Identify the current provider abstraction
- [x] Identify the current Web host and UI shell
- [x] Identify the current JSON-backed repositories
- [x] Identify the remaining parity gaps from the codebase

## Provider Flow

- [x] Authenticate against Nugs
- [x] Discover albums, artists, playlists, videos, and livestreams
- [x] Build a download plan from provider discovery
- [x] Report basic download progress
- [ ] Port the full Nugs media selection rules from the Go downloader
- [ ] Port the full Nugs naming and output-folder rules from the Go downloader
- [ ] Port resume handling and partial-file recovery from the Go downloader
- [ ] Port metadata/artwork/file-organization rules from the Go downloader
- [ ] Validate edge-case URLs against the current Go behavior

## Persistence

- [x] Persist jobs in the Web project
- [x] Persist file state in the Web project
- [x] Persist provider credentials in the Web project
- [x] Persist secret material behind an abstraction
- [ ] Replace the JSON persistence layer with the intended long-term store
- [ ] Confirm persisted state matches the migration ledger model

## UI

- [x] Show a home/dashboard page
- [x] Show job details
- [x] Show provider selection on the home page
- [x] Submit a download request from the UI
- [ ] Split queue, login, file-state, and provider-settings into dedicated Blazor pages
- [ ] Surface resume and retry actions in the job details view
- [ ] Add provider-aware capability hints to the UI

## Validation

- [x] Unit test the workflow orchestration
- [x] Unit test the Nugs provider discovery and planning surface
- [x] Unit test JSON repository round-trips
- [x] Unit test the current Blazor pages
- [ ] Add parity tests that compare current Nugs outputs against the legacy Go expectations
- [ ] Add cancellation and resume regression coverage
- [ ] Add provider-level failure mode coverage

## Exit Criteria

- [ ] A representative Nugs download path works end to end in C#.
- [ ] The C# output structure matches the current Go behavior for the supported slices.
- [ ] The remaining Go implementation can be retired without blocking the first Nugs workflows.
