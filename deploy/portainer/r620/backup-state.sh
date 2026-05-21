#!/usr/bin/env bash
set -euo pipefail

remote_host="${NUGS_R620_HOST:-192.168.0.21}"
backup_dir="${NUGS_R620_BACKUP_DIR:-/home/lancer1977/backups/nugs-downloader}"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
archive="nugs-downloader-state-${stamp}.tar.gz"

ssh "$remote_host" "mkdir -p '$backup_dir' && docker run --rm -v nugs-downloader_nugs-downloader-state:/state:ro -v '$backup_dir':/backup alpine:3.20 sh -c 'cd /state && tar -czf /backup/$archive .'"

echo "$remote_host:$backup_dir/$archive"
