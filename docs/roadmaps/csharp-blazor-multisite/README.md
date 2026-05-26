---
title: C# Blazor Multisite Rewrite
status: active
owner: @codex
priority: high
complexity: 5
created: 2026-04-26
updated: 2026-05-25
tags: [roadmap, csharp, blazor, multisite, rewrite]
---

# C# Blazor Multisite Rewrite

## Summary

Replace the legacy backend and React/Vite UI with a single C# solution built around Blazor and ASP.NET Core.

## Current Status

- The legacy tree has been removed from the repository.
- The active C# tree exists under `csharp/`.
- The Blazor web host scaffold exists and is wired into a solution file.
- The provider layer now includes the first LivePhish adapter slice alongside Nugs.
- LivePhish also has opt-in live auth/workflow smoke coverage for the representative path.
- The phase 4 LivePhish support slice and the phase 5 cleanup notes are now documented in the roadmap set.
- The Domain, App, and Infrastructure layers have initial types and ports.
- The route/menu/dashboard matrix docs now mirror the live host and dashboard cards.
- The operator/publishing runbook now captures the recurring publish, smoke, backup, and restore flows.
- The acceptance criteria doc now ties product and operational signoff to the live host, release evidence, and recovery checks.
- The repo now has a shared roadmap index and cross-repo checklist template that standardize naming and checklist structure for future items.

## Goals

- Consolidate the app into one language and one solution.
- Preserve the current nugs download behavior during migration.
- Add a provider layer so new sites can be supported without rewriting the app each time.
- Support sites such as `nugs.net` and `livephish.com` through site-specific adapters.

## Core Idea

The rewrite should not hard-code one website into the app shell. Instead:

- The Blazor UI handles login, queue management, progress, and file state.
- A backend service layer owns authentication, site detection, downloading, resume, and file organization.
- Each site implements a provider contract with its own URLs, auth flow, media extraction, and edge-case handling.

## Proposed Solution Shape

- ASP.NET Core Blazor Web App for the UI
- Host-in-process application services for downloads and state
- Shared domain models for jobs, files, credentials, and provider metadata
- Provider abstraction for `nugs.net`, `livephish.com`, and future services
- Persistent job/file state backed by SQLite or a similar local store

## Docs

- [Architecture](./architecture.md)
- [Provider Contracts](./provider-contracts.md)
- [Migration Ledger](./migration-ledger.md)
- [Release Notes](./release-notes.md)
- [Operations Runbook](./operations-runbook.md)
- [Acceptance Criteria and Owners](./acceptance-criteria.md)
- [Roadmaps Index](../README.md)
- [Nugs Parity Checklist](./nugs-parity-checklist.md)
- [UI Pages](./ui-pages.md)
- [UI Matrix](./ui-matrix.md)
- [Phases](./phases.md)
- [Risks](./risks.md)
- [Questions](./questions.md)

## Initial Scope

- First-class support for the existing Nugs workflows
- Login and credential capture for supported sites
- Local file state tracking, resume state, and job history
- One provider at a time, starting with Nugs, then LivePhish

## Non-Goals For The First Pass

- Perfect parity with every legacy edge case on day one
- Supporting arbitrary sites without provider implementation work
- Rewriting everything before proving one provider end to end
