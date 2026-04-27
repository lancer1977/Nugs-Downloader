---
title: Web UI
status: active
owner: @codex
priority: medium
complexity: 2
created: 2026-04-26
updated: 2026-04-26
tags: [feature, ui, vite, react]
---

# Web UI

## Summary

The UI is a React/Vite app whose production build is embedded into the Go backend.

## Notes

- Source lives in `ui/`
- `ui/vite.config.ts` writes the production bundle to `src/ui/dist`
- `src/ui_embedded.go` embeds that bundle into the backend binary
- `src/pkg/server` serves the embedded files and falls back to `index.html` for SPA routes
- The backend serves the UI when `--ui` is enabled

## Cleanup Checklist

- [x] Keep the UI as a separate frontend source tree
- [x] Align the production output with the backend embed path
- [x] Replace the stock Vite README with project-specific notes
- [x] Remove unused starter assets
