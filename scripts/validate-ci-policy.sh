#!/usr/bin/env bash
set -euo pipefail

# This intentionally uses only POSIX text tools so pull requests can prove
# the permission boundary without a package restore or a GitHub credential.

workflow="${1:-.github/workflows/ci.yml}"

if [[ ! -f "$workflow" ]]; then
  printf 'CI policy validation failed: workflow not found: %s\n' "$workflow" >&2
  exit 1
fi

job_block() {
  local job="$1"
  awk -v job="$job" '
    $0 == "  " job ":" { in_job = 1 }
    in_job && $0 ~ /^  [[:alnum:]_-]+:$/ && $0 != "  " job ":" { exit }
    in_job { print }
  ' "$workflow"
}

require_block() {
  local job="$1"
  local block
  block="$(job_block "$job")"
  if [[ -z "$block" ]]; then
    printf 'CI policy validation failed: job %s is missing\n' "$job" >&2
    exit 1
  fi
  printf '%s\n' "$block"
}

test_block="$(require_block test)"
container_block="$(require_block container)"
publish_block="$(require_block publish)"

for block_name in test_block container_block; do
  block="${!block_name}"
  if ! grep -Fqx '      contents: read' <<<"$block" || grep -Fqx '      packages: write' <<<"$block"; then
    printf 'CI policy validation failed: %s must be read-only\n' "${block_name%_block}" >&2
    exit 1
  fi
done

if ! grep -Fqx '      contents: read' <<<"$publish_block" || ! grep -Fqx '      packages: write' <<<"$publish_block"; then
  printf 'CI policy validation failed: publish must have contents: read and packages: write\n' >&2
  exit 1
fi

if ! grep -Fqx "    if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/master')" <<<"$publish_block"; then
  printf 'CI policy validation failed: publish must run only for trusted main/master pushes\n' >&2
  exit 1
fi

if [[ "$(grep -Fxc '      packages: write' "$workflow")" != "1" ]]; then
  printf 'CI policy validation failed: packages: write must appear exactly once\n' >&2
  exit 1
fi

if ! grep -Fqx '      run: scripts/validate-ci-policy.sh' <<<"$test_block"; then
  printf 'CI policy validation failed: test job must execute this policy check\n' >&2
  exit 1
fi

printf 'CI permission policy passed: validation jobs are read-only and only trusted publish can write packages.\n'
