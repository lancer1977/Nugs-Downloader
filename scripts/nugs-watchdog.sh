#!/usr/bin/env bash
set -euo pipefail

# LAN-only watchdog for the internal Nugs deployment.
# Quiet on success. Prints actionable, secret-free failure details on failure.

HOST="${NUGS_WATCHDOG_HOST:-nugs.polyhydragames.com}"
LAN_IP="${NUGS_WATCHDOG_LAN_IP:-192.168.0.21}"
SERVICE="${NUGS_WATCHDOG_SERVICE:-nugs-downloader_nugs-downloader-web}"
DIRECT_BASE="${NUGS_WATCHDOG_DIRECT_BASE:-http://${LAN_IP}:5107}"
HTTPS_BASE="${NUGS_WATCHDOG_HTTPS_BASE:-https://${HOST}}"
MIN_FREE_BYTES="${NUGS_WATCHDOG_MIN_FREE_BYTES:-1073741824}"
DOWNLOAD_PATH="${NUGS_WATCHDOG_DOWNLOAD_PATH:-/downloads}"
REQUIRE_DOCKER="${NUGS_WATCHDOG_REQUIRE_DOCKER:-0}"
PUBLIC_DNS_SERVERS=("${NUGS_WATCHDOG_PUBLIC_DNS_1:-1.1.1.1}" "${NUGS_WATCHDOG_PUBLIC_DNS_2:-8.8.8.8}")

failures=()

record_failure() {
  failures+=("$1")
}

check_http() {
  local url="$1"
  local expected="$2"
  local body
  if ! body="$(curl -fsS --max-time 10 "$url" 2>&1)"; then
    record_failure "HTTP check failed for ${url}: ${body}"
    return
  fi

  if [[ -n "$expected" && "$body" != *"$expected"* ]]; then
    record_failure "HTTP check returned unexpected body for ${url}: ${body:0:120}"
  fi
}

check_http "${DIRECT_BASE}/health" "Healthy"
check_http "${HTTPS_BASE}/health" "Healthy"
check_http "${HTTPS_BASE}/health/ready" "Healthy"

if ! curl -fsS --max-time 10 -o /dev/null "${HTTPS_BASE}/_framework/blazor.web.js" 2>/tmp/nugs-watchdog-curl.err; then
  record_failure "Blazor static asset check failed: $(cat /tmp/nugs-watchdog-curl.err)"
fi
rm -f /tmp/nugs-watchdog-curl.err

lan_answer="$(getent ahostsv4 "$HOST" | awk '{print $1}' | sort -u | tr '\n' ' ' | sed 's/[[:space:]]*$//')"
if [[ "$lan_answer" != "$LAN_IP" ]]; then
  record_failure "LAN DNS posture changed for ${HOST}: got '${lan_answer:-no answer}', expected '${LAN_IP}'"
fi

if command -v dig >/dev/null 2>&1; then
  for resolver in "${PUBLIC_DNS_SERVERS[@]}"; do
    public_answer="$(dig +short A "@$resolver" "$HOST" | tr '\n' ' ' | sed 's/[[:space:]]*$//')"
    if [[ -n "$public_answer" ]]; then
      record_failure "Public DNS resolver ${resolver} returned A record(s) for ${HOST}: ${public_answer}"
    fi
  done
fi

if command -v docker >/dev/null 2>&1; then
  replicas="$(docker service ls --filter "name=${SERVICE}" --format '{{.Replicas}}' 2>/dev/null || true)"
  if [[ "$replicas" != "1/1" ]]; then
    if [[ "$REQUIRE_DOCKER" == "1" ]]; then
      record_failure "Docker service ${SERVICE} replica state is '${replicas:-missing}', expected 1/1"
    fi
  fi

  container_id="$(docker ps --filter "name=${SERVICE}" --format '{{.ID}}' | head -n1)"
  free_bytes=""
  if [[ -n "$container_id" ]]; then
    free_bytes="$(docker exec "$container_id" df -PB1 "$DOWNLOAD_PATH" 2>/dev/null | awk 'NR==2 {print $4}' || true)"
  fi
  if [[ -n "$free_bytes" && "$free_bytes" =~ ^[0-9]+$ && "$free_bytes" -lt "$MIN_FREE_BYTES" ]]; then
    record_failure "Download filesystem free space below threshold: ${free_bytes} bytes available, ${MIN_FREE_BYTES} required"
  elif [[ "$REQUIRE_DOCKER" == "1" && -z "$free_bytes" ]]; then
    record_failure "Could not read ${DOWNLOAD_PATH} free space from service container ${SERVICE}"
  fi
elif [[ "$REQUIRE_DOCKER" == "1" ]]; then
  record_failure "Docker CLI is unavailable, cannot verify service replica state or download free space"
fi

if (( ${#failures[@]} > 0 )); then
  printf 'Nugs watchdog failed (%d check(s))\n' "${#failures[@]}"
  printf -- '- %s\n' "${failures[@]}"
  exit 1
fi
