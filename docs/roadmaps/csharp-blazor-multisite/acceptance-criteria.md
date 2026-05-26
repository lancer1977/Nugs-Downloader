---
title: Acceptance Criteria and Owners
status: draft
owner: @codex
priority: high
complexity: 2
created: 2026-05-25
updated: 2026-05-25
tags: [roadmap, acceptance, owners, csharp, blazor, multisite]
---

# Acceptance Criteria and Owners

This document closes the gap between the rewrite roadmap and the people who would approve a broader release.

## Purpose

The rewrite already has implementation slices, validation gates, and runbooks. What it lacked was a concise agreement on:

- what counts as *good enough* for product signoff,
- what counts as *safe enough* for operational signoff,
- and which evidence must exist before the project can be treated as release-ready.

## Owner Roles

### Product owner

The product owner signs off on user-facing readiness:

- the Nugs baseline still works end to end,
- the Blazor host presents the expected navigation and dashboard surfaces,
- provider selection and workflow entry points are understandable,
- and the rewrite no longer depends on the retired legacy UI/runtime path.

### Operational owner

The operational owner signs off on service readiness:

- build and test gates are repeatable,
- publish and restore steps are documented,
- secrets are handled through the repo’s supported local secret flow,
- and the runtime can be recovered from documented backups.

## Long-Range Acceptance Criteria

### Product acceptance

- The active host is the C# Blazor application, not the legacy Go/React stack.
- The current Nugs workflow remains the baseline for parity decisions.
- LivePhish remains a supported provider slice, with the contract and auth behavior documented.
- The visible routes, navigation labels, and dashboard sections match the live host surface.
- The feature and roadmap docs describe the same current implementation state.

### Operational acceptance

- `dotnet build csharp/NugsDownloader.sln --no-restore` succeeds.
- `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --no-restore` succeeds.
- `dotnet run --project csharp/NugsDownloader.Web` remains a valid host validation step.
- The publish/smoke/backup/restore runbook is current and usable.
- The app’s credential and state handling are documented well enough that a fresh operator can recover the stack without guessing.

### Release evidence acceptance

The following evidence should exist before a release is treated as broadly acceptable:

- a current roadmap with completed phases marked honestly,
- a release note or migration note describing the rewrite state,
- a UI matrix that reflects the live host,
- a runbook that covers operator tasks,
- and passing validation output for the repo’s native build and test gates.

## Signoff Checklist

### Product signoff

- [ ] Nugs baseline verified in the current host
- [ ] LivePhish slice acknowledged as part of the supported provider set
- [ ] UI navigation and dashboard surfaces reviewed against the live matrix
- [ ] Docs describe the active architecture without legacy-runtime drift

### Operational signoff

- [ ] Build gate passed
- [ ] Test gate passed
- [ ] Runbook reviewed for publish, smoke, backup, and restore
- [ ] Secret handling path confirmed
- [ ] Recovery path documented and understandable

## Open Decisions to Carry Forward

These questions are still useful for the next implementation slices even after the acceptance criteria are documented:

- Should file-state persistence stay in SQLite or move to a document store?
- Should downloads run inside the web app process or a hosted worker?
- Should provider selection be explicit by site, or inferred more automatically from URL patterns?
- Which parts of the UI should become dedicated pages first if scope expands again?

## Notes

This file is intentionally compact. It is meant to be the signoff anchor, not a second roadmap.
