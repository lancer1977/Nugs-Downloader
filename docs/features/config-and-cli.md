---
title: Config and Hosting
status: active
owner: @codex
priority: high
complexity: 2
created: 2026-04-26
updated: 2026-05-22
tags: [feature, config, hosting]
---

# Config and Hosting

## Summary

The app is configured through the `NugsDownloader` settings section and the deployment environment.

## Current Behavior

- Storage and persistence settings are bound from the app configuration.
- CLI-style Go flags are no longer part of the runtime.
- The web host uses the configured URL and port from the deployment environment or launch settings.
- Runtime state and data-protection keys are stored beneath the configured state directory.

## Checklist

- [x] Document the storage/config section used by the web host
- [x] Keep the deployment stack and the app configuration aligned
- [x] Replace the old Go CLI expectations with the current C# host model
