---
title: Nugs Parity Checklist
status: active
owner: @codex
priority: high
complexity: 4
created: 2026-04-26
updated: 2026-05-22
tags: [roadmap, checklist, nugs, parity, csharp]
---

# Nugs Parity Checklist

## Purpose

This checklist captures the remaining work needed to make the C# rewrite cover the current Go-era Nugs behavior closely enough to retire the legacy path.

## Scope

- Keep Nugs as the first migration target.
- Focus on behavior parity before expanding to LivePhish.
- Treat the checklist as the operational source of truth for phase 3.

## Discovery

- [x] Identify the staged C# solution layout
- [x] Identify the current provider abstraction
- [x] Identify the current Web host and UI shell
- [x] Identify the current JSON-backed repositories
- [x] Identify the remaining parity gaps from the codebase

## Provider Flow

- [x] Authenticate against Nugs
- [x] Discover albums, artists, playlists, videos, and livestreams
- [x] Build a download plan from provider discovery
- [x] Report basic download progress
- [x] The legacy comparison set has been retired.
- [x] Nugs provider behavior now uses the C# implementation as the baseline.

### Current Provider Parity Slice

- [x] Cover album, playlist, video, and livestream selection rules in C# provider tests
- [x] Cover output-root anchored file naming for audio and video files
- [x] Cover `metadata.nfo` and `cover.jpg` planning
- [x] Cover provider progress execution and cancellation behavior
- [x] Restore the C# file resume manager used by resume tests
- [x] Add workflow-level resume preparation for partial file states
- [x] Add workflow-level retry preparation for failed or paused jobs
- [x] Persist failed provider execution as failed jobs with error details
- [x] Persist cancelled execution as cancelled jobs
- [x] Re-execute a live representative Nugs download through the C# workflow
- [x] Wire full credential replay and real download continuation for resume/retry

## Persistence

- [x] Persist jobs in the Web project
- [x] Persist file state in the Web project
- [x] Persist provider credentials in the Web project
- [x] Persist secret material behind an abstraction
- [x] Protect JSON-backed secret values with ASP.NET Core Data Protection
- [x] Persist successful login credentials for later queue use
- [x] Make the Web state directory configurable for container deployment
- [x] Make the default download directory configurable for container deployment
- [x] Add readiness checks for writable state and download storage
- [x] Replace the JSON persistence layer with the intended long-term store
- [x] Confirm persisted state matches the migration ledger model

## UI

- [x] Show a home/dashboard page
- [x] Show job details
- [x] Show provider selection on the home page
- [x] Submit a download request from the UI
- [x] Split queue, login, file-state, and provider-settings into dedicated Blazor pages
- [x] Surface resume and retry actions in the job details view
- [x] Add provider-aware capability hints to the UI
- [x] Start a queued download with a stored provider account

## Validation

- [x] Unit test the workflow orchestration
- [x] Unit test the Nugs provider discovery and planning surface
- [x] Unit test JSON repository round-trips
- [x] Unit test the current Blazor pages
- [x] Add parity tests that compare current Nugs outputs against the prior baseline expectations
- [x] Add cancellation and resume regression coverage
- [x] Add provider-level failure mode coverage
- [x] Add integration tests for Blazor host routing and DI wiring
- [x] Add component coverage for login persistence and stored-account queue submission
- [x] Validate antiforgery middleware is present for interactive Razor components
- [x] Add opt-in live Nugs auth integration test using `~/.config/secrets/nugs-downloader.env`
- [x] Re-run live Nugs auth when credentials rotate

## Latest Validation

- [x] `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj -m:1 -p:UseSharedCompilation=false --blame-hang-timeout 30s`
- [x] Full C# test suite passed: 61 passed, 0 failed
- [x] Validate SQLite-backed persistence round-trips jobs, file states, credentials, and secrets
- [x] Re-ran live Nugs auth/workflow tests after the persistence swap: `NugsDownloader.Tests` `Category=LiveNugs` passed
- [x] Resolve existing package warnings for `bunit` version resolution and `Microsoft.Extensions.Caching.Memory`
- [x] Re-ran live Nugs auth tests after credentials rotation: `NugsDownloader.Tests` `Category=LiveNugs` passed
- [x] Re-executed a representative live Nugs download through the C# workflow: `NugsDownloader.Tests` `Category=LiveNugs` passed
- [x] Replayed stored credentials through `ResumeAsync` and `RetryAsync` in the C# workflow
- [x] Re-ran the Nugs media-provider plan tests after aligning output paths with the Go layout: `NugsDownloader.Tests` `FullyQualifiedName~NugsMediaProviderTests` passed

## Exit Criteria

- [x] A representative Nugs download path works end to end in C#.
- [x] The C# output structure matches the supported slices recorded during migration.
- [x] The remaining Go implementation can be retired without blocking the first Nugs workflows.
