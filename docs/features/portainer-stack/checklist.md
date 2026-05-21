---
title: Portainer Stack Checklist
status: active
owner: @codex
priority: high
complexity: 3
created: 2026-05-12
updated: 2026-05-12
tags: [checklist, deployment, portainer, r620]
---

# Portainer Stack Checklist

## Discovery

- [x] Confirmed the repo had no existing Docker or compose deployment files
- [x] Confirmed r620 is Portainer endpoint `2`
- [x] Confirmed r620 port `5107` is available
- [x] Matched the stack shape to existing r620 Swarm app stacks

## Implementation

- [x] Add Docker publish image
- [x] Add Swarm stack YAML
- [x] Add GHCR publish helper
- [x] Add deployment documentation
- [x] Publish image to GHCR
- [x] Create the Portainer stack
- [x] Configure explicit state and download directories
- [x] Add readiness health check for writable storage
- [x] Add r620 smoke helper
- [x] Add r620 state backup helper
- [x] Add guarded r620 state restore helper
- [x] Pin the live Portainer stack environment to the deployed image digest
- [x] Add r620 Traefik file-provider route for `nugs.polyhydragames.com`

## Validation

- [x] Run .NET tests
- [x] Build the Docker image
- [x] Verify stack service state in Portainer
- [x] Smoke `GET /health`
- [x] Smoke `GET /health/ready`
- [x] Smoke the Blazor UI
- [x] Smoke `https://nugs.polyhydragames.com/`
- [x] Create a live state backup from the r620 state volume

## Follow-Up

- [x] Configure LAN DNS for `nugs.polyhydragames.com`
- [ ] Decide whether `/downloads` should bind to a NAS path instead of a named volume
- [x] Add backup/restore notes for persisted credentials and job state
