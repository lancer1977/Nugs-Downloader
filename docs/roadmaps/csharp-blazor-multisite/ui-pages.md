---
title: UI Pages
status: active
owner: @codex
priority: medium
complexity: 3
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, ui, blazor, pages]
---

# UI Pages

## Initial Page Set

### Home / Dashboard

- Show current jobs
- Show recent activity
- Show provider health / last sync state

### Login

- Capture provider selection
- Capture username and password or token
- Show auth result and stored account label

### Queue

- Create a new download job from a URL
- Select output options
- Show queued, running, failed, and complete items

### Job Details

- Show source URL
- Show discovery result
- Show files and resume state
- Show retry/resume actions

### File State

- Show the state of files on disk
- Show missing, partial, complete, and stale entries
- Allow refresh from disk

### Provider Settings

- Show per-provider credentials
- Show per-provider defaults
- Show provider-specific capabilities and limits

## UI Design Rules

- Keep the UI data-driven from persisted state.
- Avoid ephemeral browser-only state for jobs.
- Keep provider-specific UI visible only when the selected provider needs it.

