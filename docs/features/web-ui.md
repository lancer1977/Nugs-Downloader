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
- Job Details and File State show actual downloaded byte counts when provider execution writes files to disk
- Live media smoke: `NUGS_LIVE_TESTS=1 dotnet test csharp/NugsDownloader.sln --filter LiveNugsWorkflowTests`; this uses local secrets, writes to a temp directory, verifies output bytes, and must keep secrets out of logs
- The Queue page labels production support honestly: Nugs and LivePhish audio write media bytes with `.partial` + atomic rename semantics; video/HLS remains disabled/labelled until implemented end-to-end
- Video/HLS remains intentionally disabled because provider-specific entitled stream URL resolution and sanitized FFmpeg progress/error capture are not verified end-to-end yet; re-enable only with unit coverage plus a gated live smoke.
- `/health/ready` checks that state and download directories are writable and that the download filesystem has at least `NugsDownloader__MinimumFreeBytes` free; the default threshold is 1 GiB

## Cleanup Checklist

- [x] Keep the UI inside the C# web host
- [x] Serve the app and UI from the same deployment surface
- [x] Replace the embedded React/Vite bundle with Blazor components
- [x] Remove the legacy Vite UI tree
