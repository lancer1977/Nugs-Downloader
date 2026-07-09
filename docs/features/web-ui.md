---
title: Web UI
status: active
owner: @codex
priority: medium
complexity: 2
created: 2026-04-26
updated: 2026-05-22
tags: [feature, ui, blazor]
---

# Web UI

## Summary

The UI is a Blazor Server app hosted by `csharp/NugsDownloader.Web`.

## Notes

- Components live under `csharp/NugsDownloader.Web/Components`
- Shared styles and static assets live under `csharp/NugsDownloader.Web/wwwroot`
- The same host serves the UI, health checks, and runtime APIs
- Primary pages include login, queue, help, job details, provider settings, and file state
- `nugs.polyhydragames.com` is LAN-only/internal-only; keep DNS and Traefik routing scoped to the LAN
- Static art assets live under `wwwroot/assets` and are used by the dashboard hero, app icon, and empty states

## Cleanup Checklist

- [x] Keep the UI inside the C# web host
- [x] Serve the app and UI from the same deployment surface
- [x] Replace the embedded React/Vite bundle with Blazor components
- [x] Remove the legacy Vite UI tree
