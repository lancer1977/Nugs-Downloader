# r620 Portainer Stack

This folder contains the deployable r620 stack for the C# Blazor app.

## Runtime Shape

- Portainer endpoint: `r620`
- Stack name: `nugs-downloader`
- Image: `ghcr.io/lancer1977/nugs-downloader-web:main`
- Container port: `8080`
- r620 published port: `5107`
- Traefik host: `nugs.polyhydragames.com`
- Persistent app state: `nugs-downloader-state:/app/state`
- Download workspace: `nugs-downloader-downloads:/downloads`
- Readiness endpoint: `/health/ready`
- Traefik dynamic route: `/home/lancer1977/servers/traefik/dynamic/nugs.yml` on r620

## Publish Checklist

- [x] Run the test suite.
- [x] Publish the image with `./deploy/portainer/r620/publish-image.sh`.
- [x] Create or update the Portainer stack from `stack.yml` on endpoint `r620`.
- [x] Verify `http://192.168.0.21:5107/health`.
- [x] Verify `http://192.168.0.21:5107/health/ready`.
- [x] Verify the UI at `http://192.168.0.21:5107/`.
- [x] Verify the Traefik route if DNS is configured.

## Live Deployment

- Stack id: `756`
- Created: `2026-05-12`
- Current smoke URL: `http://192.168.0.21:5107/`
- Current HTTPS URL: `https://nugs.polyhydragames.com/`
- Health URL: `http://192.168.0.21:5107/health`
- Image digest: `sha256:e79c54f6457a586bf486385de442d11328db50eaae63885c221c0be48d91441c`
- Latest state backup proof: `/home/lancer1977/backups/nugs-downloader/nugs-downloader-state-20260512T143313Z.tar.gz`

## Current Follow-Up

- `nugs.polyhydragames.com` is intended to stay LAN-only through Pi-hole/local DNS.
- Traefik on r620 has Docker provider disabled, so the active route is the file-provider route in `/home/lancer1977/servers/traefik/dynamic/nugs.yml`.

## Operations

Run a live smoke check:

```bash
./deploy/portainer/r620/smoke-r620.sh
```

Create a state backup on r620:

```bash
./deploy/portainer/r620/backup-state.sh
```

Restore a state backup:

```bash
NUGS_RESTORE_CONFIRM=restore ./deploy/portainer/r620/restore-state.sh /home/lancer1977/backups/nugs-downloader/<archive>.tar.gz
```

The restore script scales the Swarm service to zero, replaces the `nugs-downloader_nugs-downloader-state` volume contents, then scales the service back to one replica.

## Notes

- The image includes `ffmpeg` for media workflows.
- The stack is LAN/VPN-only by default through the Traefik IP whitelist.
- The app currently stores JSON state under `/app/state`, so that path is volume-backed.
- The current image includes the interactive Blazor boot script required for the login form submit handler.
- Login now persists username plus a protected secret, and Queue can start downloads with the stored account.
