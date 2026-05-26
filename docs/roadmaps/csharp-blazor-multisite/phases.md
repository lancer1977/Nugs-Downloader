---
title: Phases
status: active
owner: @codex
priority: high
complexity: 5
created: 2026-04-26
updated: 2026-05-25
tags: [roadmap, phases, csharp, blazor, multisite]
---

# Phases

## Phase 1: Solution Skeleton

- [x] Define the C# solution and project layout
- [x] Define the Blazor web app shell
- [x] Define shared domain models and DTOs
- [x] Define local configuration and secret handling
- [x] Define project namespaces and wiring order
- [x] Create the staged C# solution and project files

## Phase 2: Core Runtime

- [x] Define provider contracts and routing
- [x] Define job tracking and file-state persistence
- [x] Define authentication service flows
- [x] Define basic progress reporting

## Phase 3: Nugs Parity

- [x] Stand up the C# Nugs provider scaffold
- [x] Stand up the C# app workflow scaffold
- [x] Stand up the C# Blazor host and basic pages
- [x] Port current Nugs download flows end to end
- [x] Port media selection, naming, and resume behavior
- [x] Port metadata and file organization behavior
- [x] Add parity tests against the Go feature set
- [x] Validate the app against the current Go feature set

## Phase 4: LivePhish Support

- [x] Add LivePhish provider support
- [x] Model site-specific auth and media extraction
- [x] Verify state tracking and file handling against LivePhish workflows

## Phase 5: Cleanup and Retirement

- [x] Remove the Go/React implementation after parity is proven
- [x] Collapse duplicate docs into the new C# architecture docs
- [x] Add release and migration notes for the new stack
