# UI

This folder contains the React/Vite frontend for Nugs-Downloader.

## Build

```bash
npm install
npm run build
```

The production build is emitted to `src/ui/dist`, where the Go backend embeds it.

## Development

```bash
npm run dev
```

## Notes

- Keep this tree frontend-only.
- The backend owns serving, embedding, and API wiring.
- Remove starter assets here only if they are not referenced by the app.
