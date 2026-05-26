---
title: Operations Runbook
status: active
owner: @codex
priority: medium
complexity: 2
created: 2026-05-25
updated: 2026-05-25
tags: [roadmap, runbook, operations, publishing, portainer, csharp, blazor]
---

# Operations Runbook

This page covers the recurring publish and operator flows for the C# Blazor multisite stack.

## When to use

- Before a publish or stack refresh
- After code or configuration changes that affect the live host
- When backing up or restoring persisted state
- When checking the stack after a report about login, queue, file-state, or storage behavior

## Publish flow

1. Run the repo-native validation from the repository root:
   - `dotnet build csharp/NugsDownloader.sln --no-restore`
   - `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --filter LivePhishMediaProviderTests`
   - `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --no-restore`
2. Publish the image with `./deploy/portainer/r620/publish-image.sh`.
3. Update the Portainer stack from `deploy/portainer/r620/stack.yml` on endpoint `r620`.
4. Verify the live surface:
   - `http://192.168.0.21:5107/health`
   - `http://192.168.0.21:5107/health/ready`
   - `http://192.168.0.21:5107/`
   - `https://nugs.polyhydragames.com/`

## Routine operator checks

- Run `./deploy/portainer/r620/smoke-r620.sh` for a quick live smoke.
- Confirm the host renders the Blazor app shell and dashboard cards.
- Check readiness again if the state or download directories changed.
- Use Portainer to confirm the service is healthy and the deployed image matches the expected digest.

## Backup

- Run `./deploy/portainer/r620/backup-state.sh`.
- Save the tarball path as the backup proof for the live state volume.
- Treat the archive as the source of truth for future restores.

## Restore

- Restore with `NUGS_RESTORE_CONFIRM=restore ./deploy/portainer/r620/restore-state.sh /home/lancer1977/backups/nugs-downloader/<archive>.tar.gz`.
- The restore helper scales the Swarm service to zero, replaces the `nugs-downloader_nugs-downloader-state` volume contents, and scales the service back to one replica.
- Use only with the intended archive and a deliberate confirmation value.

## Release-ready checklist

- [ ] Build passed
- [ ] Tests passed
- [ ] Image published
- [ ] Portainer stack updated
- [ ] `/health` is healthy
- [ ] `/health/ready` is healthy
- [ ] UI responds on the direct port
- [ ] Traefik route responds when DNS is configured
- [ ] A fresh state backup exists after the live deployment

## Notes

- Stack name: `nugs-downloader`
- Persistent state path: `/app/state`
- Downloads path: `/downloads`
- r620 direct smoke port: `5107`
- Traefik routing on r620 uses the file-provider route, not Docker provider discovery
- Keep credentials and backup archive names out of docs and chat when they are sensitive
