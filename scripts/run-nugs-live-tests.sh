#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
secrets_file="${NUGS_SECRETS_FILE:-/home/lancer1977/.config/secrets/nugs-downloader.env}"

if [[ ! -f "$secrets_file" ]]; then
  echo "Missing Nugs secrets file: $secrets_file" >&2
  exit 2
fi

export NUGS_LIVE_TESTS=1
export NUGS_SECRETS_FILE="$secrets_file"

dotnet test "$repo_root/csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj" \
  -m:1 \
  -p:UseSharedCompilation=false \
  --filter 'Category=LiveNugs' \
  --logger 'console;verbosity=minimal'
