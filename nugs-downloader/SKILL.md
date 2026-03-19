---
name: nugs-downloader
description: Download high-quality audio and video from nugs.net. Use when users provide nugs.net URLs (release, artist, playlist, video, webcast) and want to download content in specific formats (ALAC, FLAC, MQA, AAC) or video resolutions (480p up to 4K).
---

# Nugs Downloader

Downloader for nugs.net written in Go.

## Features

- **Artist/Album Organization**: Automatically creates a nested directory structure: `/ArtistName/AlbumName`.
- **Show Metadata**: Generates a `README.md` in each album folder containing detailed metadata about the show/album.
- **High Quality**: Supports ALAC, FLAC, MQA, and 360 Reality Audio.
- **Video Support**: Downloads videos in resolutions up to 4K.

## Setup Requirements

- **Binary**: `nugs-downloader` (typically in PATH or `~/bin`).
- **Config**: `config.json` should be in `~/.nugs-downloader/config.json` (created automatically with a `README.txt` explanation on first run), the same directory as the binary, or the current working directory.
- **FFmpeg**: Required for video processing and HLS tracks. Assumed to be in system PATH by default.

## Configuration (config.json)

| Option | Description |
| --- | --- |
| email | Your nugs.net email address. |
| password | Your nugs.net password. |
| format | Quality: 1=ALAC, 2=FLAC, 3=MQA, 4=360RA, 5=150Kbps AAC. |
| videoFormat | Resolution: 1=480p, 2=720p, 3=1080p, 4=1440p, 5=4K. |
| outPath | Directory to save downloads. |
| useFfmpegEnvVar | Set to `true` to use system PATH for FFmpeg. |

## Usage Examples

Args take priority over `config.json`.

### Download Albums
```bash
nugs-downloader https://play.nugs.net/release/23329
```

### Download Multiple Items
```bash
nugs-downloader https://play.nugs.net/release/23329 https://play.nugs.net/release/23790
```

### Download with Overrides
```bash
nugs-downloader --format 2 --videoformat 3 --outpath "./MyDownloads" https://play.nugs.net/release/23329
```

### Options Reference

- `-f, --format`: Audio format (1-5).
- `-F, --videoformat`: Video format (1-5).
- `-o, --outpath`: Output path.
- `--force-video`: Forces video when audio co-exists.
- `--skip-videos`: Skips videos in artist URLs.
- `--skip-chapters`: Skips chapters for videos.

## Error Handling

If authentication fails, ensure credentials in `config.json` are correct or use a `--token` override if available. For video errors, verify `ffmpeg` is installed and accessible in the system PATH.
