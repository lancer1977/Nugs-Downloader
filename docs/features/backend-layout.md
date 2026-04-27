---
title: Backend Layout
status: active
owner: @codex
priority: high
complexity: 2
created: 2026-04-26
updated: 2026-04-26
tags: [feature, backend, layout]
---

# Backend Layout

## Summary

The Go backend now lives under `src/` as the executable entrypoint for both CLI and UI modes.

## Current Flow

- `src/main.go` parses config and routes into CLI or UI mode.
- `src/pkg/config` owns config loading, CLI parsing, and defaults.
- `src/pkg/server` serves the API and embedded UI.
- `src/pkg/processor` orchestrates downloads and media handling.
- `src/pkg/downloader` performs the fetch and resume logic.
- `src/ui_embedded.go` embeds the built frontend from `src/ui/dist`.

## Checklist

- [x] Backend entrypoint isolated under `src/`
- [x] Build tooling points at `./src`
- [x] Embedded UI path matches the backend directory
- [x] Backend helper packages moved under `src/pkg/`
