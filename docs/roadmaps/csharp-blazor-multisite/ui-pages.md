---
title: UI Pages
status: active
owner: @codex
priority: medium
complexity: 3
created: 2026-04-26
updated: 2026-05-12
tags: [roadmap, ui, blazor, pages]
---

# UI Pages

## Initial Page Set

### Home / Dashboard

- [x] Show current jobs
- [x] Show recent activity
- [x] Show provider health / last sync state through provider capability summaries

### Login

- [x] Capture provider selection
- [x] Capture username and password or token
- [x] Show auth result and stored account label
- [x] Save successful login credentials to the provider account store
- [x] Avoid rendering raw secret references or tokens in the auth result

### Queue

- [x] Create a new download job from a URL
- [x] Start a download with a stored provider account
- [x] Fall back to manual username/password/token entry when no account is stored
- [x] Select output options
- [x] Show queued, running, failed, and complete items

### Job Details

- [x] Show source URL
- [x] Show discovery result
- [x] Show files and resume state
- [x] Show retry/resume actions

### File State

- [x] Show the state of files on disk
- [x] Show missing, partial, complete, and stale entries
- [x] Allow refresh from disk

### Provider Settings

- [x] Show per-provider credentials
- [x] Show per-provider defaults
- [x] Show provider-specific capabilities and limits

## UI Design Rules

- Keep the UI data-driven from persisted state.
- Avoid ephemeral browser-only state for jobs.
- Keep provider-specific UI visible only when the selected provider needs it.
