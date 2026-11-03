# Synthetic MP4 regression fixtures

Hermes Kanban: `t_fa47380a`.

These are generated 440 Hz tones, not downloaded or provider-owned recordings.
Both contain a short playable AAC audio stream in MP4 with major brand `mp42`
and compatible brands `mp42iso2mp41`. Tests copy them to the output directory;
FFmpeg is only needed to regenerate or independently decode them.

Generate `synthetic-mp42.mp4` from this directory:

```bash
ffmpeg -hide_banner -loglevel error \
  -f lavfi -i 'sine=frequency=440:sample_rate=44100:duration=0.08' \
  -c:a aac -b:a 32k -brand mp42 -map_metadata -1 \
  -fflags +bitexact -flags:a +bitexact -y synthetic-mp42.mp4
```

Generate the leading-padding variant with Python:

```python
from pathlib import Path
import struct

payload = Path('synthetic-mp42.mp4').read_bytes()
ftyp_size = struct.unpack('>I', payload[:4])[0]
assert payload[4:8] == b'ftyp'
assert payload[ftyp_size + 4:ftyp_size + 8] == b'free'
free_size = struct.unpack('>I', payload[ftyp_size:ftyp_size + 4])[0]
Path('synthetic-free-mp42.mp4').write_bytes(
    payload[ftyp_size:ftyp_size + free_size]
    + payload[:ftyp_size]
    + payload[ftyp_size + free_size:])
```

Reordering the existing adjacent `ftyp`/`free` boxes, rather than prepending new
bytes, preserves all later offsets including media chunk offsets in `moov`.
Validate both payloads (both were decoded successfully during this repair):

```bash
for file in synthetic-mp42.mp4 synthetic-free-mp42.mp4; do
  ffprobe -v error -show_entries stream=codec_name,codec_type \
    -show_entries format_tags=major_brand,compatible_brands -of json "$file"
  ffmpeg -v error -i "$file" -f null - || exit 1
done
```
