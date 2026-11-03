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

The detector reads at most the first 4096 bytes and walks at most 32 top-level
ISO BMFF boxes. It skips only leading `free`, `skip`, and `wide` padding boxes;
it does not search arbitrary payload bytes for `ftyp`. Ordinary and extended
64-bit lengths are checked against the buffered bytes before slicing or advancing.
Zero/to-EOF lengths, truncated headers/payloads, undersized boxes, misaligned
brand tables, and scan-budget exhaustion resolve conservatively to `.bin`.
No allocation or seek is driven by an untrusted box length.

A complete `ftyp` major or compatible brand `M4A ` selects `.m4a`; `isom`,
`iso2`, `mp41`, and `mp42` select `.mp4`. The minor-version field is not a brand.
Recognized HEIF/HEIC and AVIF image brands in either position veto audio naming,
even alongside an MP4 brand or conflicting audio MIME/URL. Unknown `ftyp` brands
also resolve to `.bin`, explicitly suppressing metadata fallback. This is a
container filename decision, not proof of an audio track or its codec.

Other signatures remain `fLaC` as FLAC, `RIFF` plus `WAVE` as WAV, and supported
ADTS sync bytes (`FF F1` or `FF F9`) as raw AAC. Recognized signatures take
precedence over conflicting URL and MIME labels.

Supported MIME fallbacks are case-insensitive: `audio/mp4` → `.m4a`,
`audio/flac` → `.flac`, and `audio/mpeg` → `.mp3`. Other MIME values, including
`application/octet-stream` and `audio/aac`, do not independently prove a suffix.
When bytes do not identify a supported signature or BMFF container, supported
MIME then URL suffixes may be used; without either, `.bin` records uncertainty.

This is bounded filename detection, not codec inspection, full media validation,
or proof that a file is playable. Unsupported containers may end up as `.bin`;
metadata can be incorrect for otherwise unidentified bytes. Existing files with
a different old suffix are not migrated or removed; a successful rename may
overwrite an existing file at the resolved destination, following the existing
download replacement policy.

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
failure, empty responses, interrupted/cancelled transfers, and final-move failure.
Two checked-in synthetic AAC/MP4 fixtures (major brand `mp42`, with and without
leading `free`) are playable and require no FFmpeg at test runtime; see their
[fixture provenance](../../csharp/tests/NugsDownloader.Tests/Fixtures/README.md).
Other signature and malformed-box fixtures are minimal headers, not recordings.
No live authentication, downloads, or production deployment is required or
certified by these tests.
