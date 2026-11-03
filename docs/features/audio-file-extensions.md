# Audio filenames and container detection

Hermes Kanban: `t_fa47380a`.

Nugs and LivePhish audio downloads keep the downloaded bytes unchanged. Requested
formats select provider quality; they do not prove the returned container and do
not trigger transcoding. In particular, requesting AAC or ALAC does not justify
naming an MP4-family container `.aac` or `.alac`.

## Filename policy

- Planning uses a recognized suffix from the media URL's **path**, then a
  recognized suffix already on the track name, otherwise `.bin`. URL queries
  (including signed tokens that contain apparent extensions) are ignored.
- After a successful, nonempty transfer, the final suffix comes from the first
  available source in this order: recognized file signature, supported response
  Content-Type, recognized actual download URL path, otherwise `.bin`.
  A provisional title suffix or requested format is not a final fallback.
- Recognized filename suffixes are `.m4a`, `.mp4`, `.aac`, `.flac`, `.mp3`,
  `.wav`, `.ogg`, `.opus`, `.aif`, and `.aiff`, case-insensitively and normalized
  to lowercase. Existing recognized suffixes or `.bin` are replaced rather than
  duplicated. Other periods in titles (for example `Song. Part`) are preserved.
- Transfers write to the provisional path plus `.partial`. Only a successful,
  nonempty transfer is renamed to its resolved final path. Failed or empty
  transfers remove the temporary file and preserve an existing target.
- Resolved paths retain their file-state IDs and are persisted by the workflow,
  including successful earlier tracks when a later track fails. A failed job's
  file states remain Partial; final paths and byte counts still identify the
  files actually written.

## Signature and fallback limits

The detector reads at most the first 16 bytes. It recognizes `fLaC` as FLAC,
`RIFF` plus `WAVE` as WAV, an `ftyp` box with major brand `M4A ` as M4A,
`ftyp` with major brand `isom` as MP4, and the supported ADTS sync bytes
(`FF F1` or `FF F9`) as raw AAC. A generic `ftyp` box is not sufficient: other
brands, including HEIF, do not establish an audio container. Recognized
signatures take precedence over conflicting URL and MIME labels.

Supported MIME fallbacks are `audio/mp4` → `.m4a`, `audio/flac` → `.flac`, and
`audio/mpeg` → `.mp3`. Other MIME values, including `application/octet-stream`
and `audio/aac`, do not independently prove a suffix. Without a recognized
signature or supported MIME type, the URL suffix is used; without that, `.bin`
explicitly records uncertainty rather than inventing an audio format.

This is bounded filename detection, not codec inspection, full media validation,
or proof that a file is playable. Unsupported MP4 brands and other audio
signatures may rely on MIME/URL metadata or end up as `.bin`. Metadata can be
incorrect when a signature is not recognized. Existing files with a different
old suffix are not migrated or removed; a successful rename may overwrite an
existing file at the resolved destination, following the existing download
replacement policy.

## Offline verification

Run with live tests disabled **and** both secret-file paths redirected; test
loaders may inspect those paths before the live-test flag:

```bash
export NUGS_LIVE_TESTS=0 LIVEPHISH_LIVE_TESTS=0
export NUGS_SECRETS_FILE=/dev/null LIVEPHISH_SECRETS_FILE=/dev/null

dotnet test csharp/NugsDownloader.sln --no-restore \
  --filter 'FullyQualifiedName~AudioFileExtensionTests|FullyQualifiedName~NugsMediaProviderTests|FullyQualifiedName~LivePhishMediaProviderTests|FullyQualifiedName~DownloadWorkflowTests'
dotnet test csharp/NugsDownloader.sln --no-restore
dotnet build csharp/NugsDownloader.sln --no-restore
```

The regression fixtures exercise both providers, extensionless entitled URLs,
signature/metadata conflicts, dotted titles, persisted paths after a later-track
failure, empty responses, and interrupted transfers. Header fixtures are minimal
container bytes, not playable recordings. No live authentication, downloads, or
production deployment is required or certified by these tests.
