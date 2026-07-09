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

The live smoke uses `/home/lancer1977/.config/secrets/nugs-downloader.env` by default and must never print credentials or tokens. It writes to a temporary output root, verifies at least one Nugs media file contains bytes, then cleans the temp directory.

```bash
NUGS_LIVE_TESTS=1 dotnet test csharp/NugsDownloader.sln --filter LiveNugsWorkflowTests
```

## Run

```bash
dotnet run --project csharp/NugsDownloader.Web
```

## Config

The app reads configuration from the `NugsDownloader` settings section and persists runtime state under the configured state directory. See the feature docs and deployment notes for the current paths and environment variables.

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
- Nugs audio jobs now write provider media bytes with `.partial` + atomic rename semantics, metadata sidecars, and byte counts into file state. The current live smoke proves a representative Nugs audio path; full-quality subscription URL expansion and LivePhish byte parity still need provider-specific hardening.
- Video/HLS controls are intentionally disabled/labelled until stream URL resolution and FFmpeg processing are production-ready.
- FFmpeg is required for video workflows and HLS-to-MP4 conversion.
