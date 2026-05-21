# Nugs-Downloader

Nugs-downloader written in Go with an embedded React UI.

## Layout

- `src/` - Go backend entrypoint and embedded UI bridge
- `src/pkg/` - backend packages shared by CLI and UI mode
- `ui/` - React/Vite frontend source
- `docs/` - repo feature and roadmap notes

## Build

```bash
make build
```

This builds the UI first, writes the production assets to `src/ui/dist`, and then builds the backend from `./src`.

## UI Setup

The UI is a separate React/Vite project under `ui/`.

Build flow:

1. `npm run build` in `ui/` emits the production bundle into `src/ui/dist`.
2. `src/ui_embedded.go` embeds `src/ui/dist` into the backend binary.
3. `src/pkg/server` serves those embedded files when you run `go run ./src --ui` or launch the built binary with `--ui`.

Runtime flow:

- `src/main.go` decides between CLI mode and UI mode.
- UI requests hit the Go server under `/api/*`.
- Non-API requests are served from the embedded bundle with SPA fallback to `index.html`.

## Run

CLI mode:

```bash
go run ./src -- https://play.nugs.net/release/23329
```

UI mode:

```bash
go run ./src --ui --port 8080
```

## Config

The app reads `config.json` from one of these locations:

- `~/.config/nugs-downloader/config.json`
- the current working directory
- the backend binary directory

If no home config exists, the app creates `~/.config/nugs-downloader/config.json` and a short `README.txt` on first run.

## Common Commands

```bash
make test
make test-coverage
make build-all
make clean
```

## Documentation

- [`docs/features/README.md`](./docs/features/README.md)
- [`docs/features/backend-layout.md`](./docs/features/backend-layout.md)
- [`docs/features/web-ui.md`](./docs/features/web-ui.md)
- [`docs/features/config-and-cli.md`](./docs/features/config-and-cli.md)
- [`docs/features/portainer-stack/README.md`](./docs/features/portainer-stack/README.md)
- [`docs/roadmaps/csharp-blazor-multisite/README.md`](./docs/roadmaps/csharp-blazor-multisite/README.md)

## Notes

- FFmpeg is required for video workflows and HLS-to-MP4 conversion.
- The UI is embedded into the backend binary at build time.
