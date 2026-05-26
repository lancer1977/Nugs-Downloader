---
title: Feature Index
status: active
owner: @codex
priority: high
complexity: 2
created: 2026-04-26
updated: 2026-05-25
tags: [documentation, Nugs-Downloader]
---

# Feature Index

This folder tracks the active C# application surfaces and the cleanup work around them.

## Current Structure

- [C# Multisite Rewrite](./csharp-multisite-rewrite.md)
- [Backend Layout](./backend-layout.md)
- [Web UI](./web-ui.md)
- [Config and Hosting](./config-and-cli.md)
- [Portainer Stack](./portainer-stack/README.md)

## Cleanup Checklist

- [x] Move the active runtime to the C# solution under `csharp/`
- [x] Replace the old Vite app with the Blazor web host
- [x] Update the repo README to match the active C# layout
- [x] Remove the legacy backend tree from the repository
