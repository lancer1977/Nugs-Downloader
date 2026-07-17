#!/usr/bin/env bash
set -euo pipefail

policy_script="$(dirname "$0")/validate-ci-policy.py"

python3 "$policy_script" .github/workflows/ci.yml

for fixture in scripts/testdata/ci-policy/*-packages-write.yml; do
  if python3 "$policy_script" "$fixture"; then
    printf 'CI policy test failed: invalid fixture was accepted: %s\n' "$fixture" >&2
    exit 1
  fi
done

printf 'CI policy negative fixtures passed.\n'
