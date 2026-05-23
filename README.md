# Nugs-Downloader

The active Nugs application lives in `csharp/` as a .NET 10 solution with a Blazor web UI.

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

## Notes

- The legacy Go backend and embedded React/Vite UI have been removed.
- The Portainer deployment targets the C# web app.
- FFmpeg is required for media workflows and HLS-to-MP4 conversion.
