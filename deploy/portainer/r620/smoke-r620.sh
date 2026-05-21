#!/usr/bin/env bash
set -euo pipefail

base_url="${NUGS_R620_BASE_URL:-http://192.168.0.21:5107}"

curl -fsS "$base_url/health"
printf '\n'
curl -fsS "$base_url/health/ready"
printf '\n'
curl -fsSI "$base_url/_content/MudBlazor/MudBlazor.min.css" | sed -n '1,6p'
curl -fsS "$base_url/" | grep -E 'NugsDownloader|mud-theme-provider|--mud-palette-background' >/dev/null

echo "Smoke passed: $base_url"
