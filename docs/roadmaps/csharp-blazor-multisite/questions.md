---
title: Questions
status: active
owner: @codex
priority: medium
complexity: 3
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, questions, csharp, blazor, multisite]
---

# Questions

## Product Scope

- Which site should be the first migration target after Nugs parity?
- Do we want one shared credential vault or per-site secret storage?
- Should the app support accounts per provider, or a single profile with multiple site credentials?

## Architecture

- Should file-state persistence live in SQLite or a JSON document store?
- Should downloads run inside the web app process or in a hosted worker/background service?
- Do we want provider discovery to be automatic or explicitly selected by URL/site?

## UI

- Which parts of the current UI should become dedicated Blazor pages first?
- Do we want server-side Blazor only, or a hybrid that keeps some client-side interactivity?

