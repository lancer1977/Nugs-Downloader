---
title: Config and CLI
status: active
owner: @codex
priority: high
complexity: 2
created: 2026-04-26
updated: 2026-04-26
tags: [feature, cli, config]
---

# Config and CLI

## Summary

The app supports both direct CLI downloads and a UI mode, with config loaded from `config.json`.

## Current Behavior

- CLI arguments override config file values.
- Config defaults are created under `~/.config/nugs-downloader/` when missing.
- `--ui` switches the app into web-server mode.
- `--port` selects the UI port.

## Checklist

- [x] Document the config search order in the root README
- [x] Keep CLI and UI mode in the same executable
- [x] Replace the old placeholder feature pages with links into this doc set
- [x] Capture the current config and FFmpeg setup in the root README
