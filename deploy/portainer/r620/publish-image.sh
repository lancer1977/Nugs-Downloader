#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
image="${NUGS_DOWNLOADER_IMAGE:-ghcr.io/lancer1977/nugs-downloader-web:main}"
extra_tag="${NUGS_DOWNLOADER_EXTRA_TAG:-}"

tags=(-t "$image")
if [[ -n "$extra_tag" ]]; then
  tags+=(-t "$extra_tag")
fi

docker buildx build \
  --platform "${NUGS_DOWNLOADER_PLATFORM:-linux/amd64}" \
  -f "$repo_root/deploy/portainer/r620/Dockerfile" \
  "${tags[@]}" \
  --push \
  "$repo_root"
