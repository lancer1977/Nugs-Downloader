---
title: Provider Contracts
status: active
owner: @codex
priority: high
complexity: 4
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, providers, csharp, multisite]
---

# Provider Contracts

## Purpose

The app should support multiple download sites without baking site-specific logic into the UI or workflow layer.

## Provider Responsibilities

Each provider should own:

- URL recognition
- Authentication flow
- Media discovery
- Site-specific format selection
- Download planning
- Resume logic
- Site-specific naming or metadata rules where needed

## Shared Inputs

- `Credentials`
- `Uri`
- `DownloadPreferences`
- `CancellationToken`

## Shared Outputs

- `AuthResult`
- `MediaDiscoveryResult`
- `DownloadPlan`
- `DownloadProgress`
- `ProviderCapabilities`

## Capability Examples

- audio only
- video support
- chapter support
- resumable downloads
- account-based access
- token-based auth

## Nugs and LivePhish Notes

- Nugs and LivePhish should both implement the same contract.
- Differences in authentication, media discovery, and manifest parsing should stay inside provider implementations.
- Shared download mechanics should be reused where possible, but provider-specific adapters may still need custom manifest handling.

