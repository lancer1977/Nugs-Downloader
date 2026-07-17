# Nugs-Downloader

The active Nugs application lives in `csharp/` as a .NET 10 solution with a Blazor web UI.

## Access policy

`nugs.polyhydragames.com` is an internal/LAN-only service. Do not add public DNS records or remove Traefik IP whitelisting for this app unless that policy is explicitly reversed.

## Tags

- nugs-downloader
- dotnet
- docs
- nugs
- downloader
- website

## Layout

- `csharp/` - active solution, web host, application logic, infrastructure, and tests
- `docs/` - feature notes, roadmaps, and deployment docs
- `deploy/` - Portainer and host deployment assets
- `scripts/` - helper scripts for local and live validation
- `README.md` - repo entrypoint and quickstart

## Build

```bash
dotnet build csharp/NugsDownloader.sln
```

## Test

```bash
dotnet test csharp/NugsDownloader.sln
```

### Live media smoke

The live smoke uses `/home/lancer1977/.config/secrets/nugs-downloader.env` by default and must never print credentials or tokens. It writes to a temporary output root, verifies at least one Nugs media file contains bytes, then cleans the temp directory even when an assertion fails. Set `NUGS_LIVE_DOWNLOAD_URL` to override the small representative release when entitlement coverage changes.

```bash
NUGS_LIVE_TESTS=1 dotnet test csharp/NugsDownloader.sln --filter LiveNugsWorkflowTests
```

## CI/CD and watchdog

GitHub Actions builds and tests the solution, runs the .NET vulnerable-package check, and builds the R620 container image for pull requests. Those validation jobs have read-only repository access and cannot publish packages. A separate trusted-push job receives package-write authority only for pushes to `main` or `master`, where it pushes an immutable `ghcr.io/lancer1977/nugs-downloader-web:<commit-sha>` tag.

The live LAN-only watchdog is `scripts/nugs-watchdog.sh`. It is quiet when healthy and prints only secret-free failure details. It checks:

- `/health`, `/health/ready`, and `/_framework/blazor.web.js`
- LAN DNS for `nugs.polyhydragames.com -> 192.168.0.21`
- public DNS posture when `dig` is installed
- Swarm replica state and download free-space threshold when Docker is available (`NUGS_WATCHDOG_REQUIRE_DOCKER=1` makes those checks mandatory on the Swarm manager)

Rollback options:

```bash
# Prefer GitOps: set NUGS_DOWNLOADER_IMAGE to the previous immutable digest/tag and redeploy the stack.
docker service update --image ghcr.io/lancer1977/nugs-downloader-web:<previous-sha-or-digest> nugs-downloader_nugs-downloader-web
docker service rollback nugs-downloader_nugs-downloader-web
curl -fsS https://nugs.polyhydragames.com/health/ready
```

## Run

```bash
dotnet run --project csharp/NugsDownloader.Web
```

## Config

The app reads configuration from the `NugsDownloader` settings section and persists runtime state under the configured state directory. See the feature docs and deployment notes for the current paths and environment variables.

Key settings:

- `NugsDownloader__StateDirectory` - app database, job state, credentials, and data-protection keys.
- `NugsDownloader__DownloadDirectory` - downloaded media archive root.
- `NugsDownloader__MinimumFreeBytes` - readiness threshold for free space on the downloads filesystem; defaults to 1 GiB.
- `NugsDownloader__BackgroundQueue__Capacity` - max queued background requests before producers wait; defaults to 2.
- `NugsDownloader__BackgroundQueue__Concurrency` - number of background download workers; defaults to 1.

## Common Commands

```bash
make test
make build
make clean
make lint
```

## Documentation

- [`docs/features/README.md`](./docs/features/README.md)
- [`docs/features/config-and-cli.md`](./docs/features/config-and-cli.md)
- [`docs/features/backend-layout.md`](./docs/features/backend-layout.md)
- [`docs/features/web-ui.md`](./docs/features/web-ui.md)
- [`docs/features/portainer-stack/README.md`](./docs/features/portainer-stack/README.md)
- [`docs/roadmaps/csharp-blazor-multisite/README.md`](./docs/roadmaps/csharp-blazor-multisite/README.md)
- [Docs Index](./docs/README.md)

## Notes

- The legacy Go backend and embedded React/Vite UI have been removed.
- The Portainer deployment targets the C# web app.
- Nugs and LivePhish audio jobs now write provider media bytes with `.partial` + atomic rename semantics, metadata sidecars, and byte counts into file state. The current live smoke proves a representative Nugs audio path; full-quality subscription URL expansion still needs provider-specific endpoint hardening.
- Video/HLS controls are intentionally disabled/labelled until stream URL resolution and FFmpeg processing are production-ready; do not re-enable video controls without an end-to-end live smoke.
- FFmpeg is required for video workflows and HLS-to-MP4 conversion.
