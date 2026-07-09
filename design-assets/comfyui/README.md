# ComfyUI Nugs Downloader Asset Concepts

Generated against the local ComfyUI instance at `http://192.168.0.173:8188` on 2026-07-08.

## Candidate assets

- `nugs-hero-concert_00001_.png` — best hero candidate; vinyl/music visual with teal/amber gradient and open left-side space for UI copy.
- `nugs-icon_00001_.png` — best app/icon candidate; pixel-art guitar/music mark with no obvious text or logo.
- `nugs-empty-queue_00001_.png` — usable empty-state concept; abstract monitor/download scene, but pink-heavy compared with the current app palette.
- `nugs-hero_00001_.png` — abstract waveform/noise texture; useful as subtle background treatment, less useful as standalone hero art.
- `nugs-empty-state_00001_.png` — weaker empty-state concept; reads more like landscape/desk texture than music/download UI.

## Recommended next UI pass

1. Add selected files under `csharp/NugsDownloader.Web/wwwroot/assets/` after choosing final candidates.
2. Use `nugs-hero-concert_00001_.png` as a dashboard hero background with a dark overlay.
3. Use `nugs-icon_00001_.png` for favicon/app identity after resizing/cropping.
4. Either regenerate or stylize `nugs-empty-queue_00001_.png` to better match the dark teal/amber palette before using it in empty queue/file-state views.

## Prompts

### Hero concept

`dark modern web app hero background, stylized jam band concert archive, glowing teal and amber audio waveforms flowing into a download tray, vinyl record, cassette, hard drive, subtle pixel art influence, clean composition, empty space on left for UI text, no words, no logos`

### Icon concept

`pixel art app icon for music downloader, stylized nugget shaped guitar pick with waveform, headphones, teal amber highlights, dark background, centered, no text, no logos, crisp edges`

### Empty queue concept

`dark UI empty state illustration, music download queue is empty, headphones on a desk, glowing waveform on monitor, concert ticket, hard drive, teal and amber accent lights, centered simple vector-like illustration, no words, no logos`
