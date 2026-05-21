#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /remote/path/to/nugs-downloader-state-YYYYMMDDTHHMMSSZ.tar.gz" >&2
  exit 2
fi

if [[ "${NUGS_RESTORE_CONFIRM:-}" != "restore" ]]; then
  echo "Set NUGS_RESTORE_CONFIRM=restore to replace the live state volume." >&2
  exit 2
fi

remote_host="${NUGS_R620_HOST:-192.168.0.21}"
archive_path="$1"
archive_dir="$(dirname "$archive_path")"
archive_name="$(basename "$archive_path")"
service="nugs-downloader_nugs-downloader-web"
volume="nugs-downloader_nugs-downloader-state"

ssh "$remote_host" "
set -euo pipefail
test -f '$archive_path'
docker service scale '$service'=0
docker run --rm -v '$volume':/state -v '$archive_dir':/backup alpine:3.20 sh -c 'rm -rf /state/* && tar -xzf /backup/$archive_name -C /state'
docker service scale '$service'=1
"
