---
title: Portainer Stack
status: active
owner: @codex
priority: high
complexity: 3
created: 2026-05-12
updated: 2026-05-12
tags: [feature, deployment, portainer, r620, blazor]
---

# Portainer Stack

## Summary

The C# Blazor host can be deployed to the r620 Portainer environment as a Swarm stack named `nugs-downloader`.

## Current State

- [x] Added a .NET 10 runtime image for `NugsDownloader.Web`
- [x] Added a Swarm stack file for r620
- [x] Persisted app JSON state through a Docker volume
- [x] Reserved r620 ingress port `5107`
- [x] Added a publish helper for GHCR image updates
- [x] Created the live Portainer stack
- [x] Smoke-tested the live UI and health endpoint
- [x] Added configurable state and download directories
- [x] Added writable storage readiness checks
- [x] Added r620 smoke, backup, and restore helpers
- [x] Configure LAN DNS and Traefik file-provider route for `nugs.polyhydragames.com`

## Runtime Contract

- Stack: `nugs-downloader`
- Service: `nugs-downloader-web`
- Image: `ghcr.io/lancer1977/nugs-downloader-web:main`
- Published port: `5107`
- HTTPS route: `https://nugs.polyhydragames.com/`
- Health endpoint: `/health`
- Readiness endpoint: `/health/ready`
- Persistent state: `/app/state`
- Download workspace: `/downloads`
- Live stack id: `756`
- Current image digest: `sha256:e79c54f6457a586bf486385de442d11328db50eaae63885c221c0be48d91441c`
- Latest state backup proof: `/home/lancer1977/backups/nugs-downloader/nugs-downloader-state-20260512T143313Z.tar.gz`

## Implementation Notes

- Deployment files live under `deploy/portainer/r620/`.
- The stack follows the newer r620 Swarm pattern with Traefik labels and an internal-only IP whitelist.
- The direct port exists so the deployment can be verified without relying on DNS or Traefik.
- The r620 Traefik container has Docker provider disabled, so the working hostname route is file-provider config at `/home/lancer1977/servers/traefik/dynamic/nugs.yml`.
- `http://192.168.0.21:5107/health` returned `Healthy` after deployment.
- `http://192.168.0.21:5107/health/ready` verifies the state and download directories are writable.
- `http://192.168.0.21:5107/` rendered `NugsDownloader` and included MudBlazor dark theme variables.
- `https://nugs.polyhydragames.com/` rendered `NugsDownloader` after adding the file-provider route.
- `https://nugs.polyhydragames.com/login` includes the interactive server render marker and `_framework/blazor.web.js`, which are required for the Authenticate button to invoke the Blazor submit handler.
- The live image persists successful login credentials with protected secret values and lets Queue start a download using a stored provider account.
- `deploy/portainer/r620/backup-state.sh` creates a remote tarball from the live state volume.
- `deploy/portainer/r620/restore-state.sh` restores that state volume behind an explicit confirmation environment variable.
- The Portainer stack environment is pinned to the current image digest even though the repo stack file defaults to the `main` tag for normal publish flow.
