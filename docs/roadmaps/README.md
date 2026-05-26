---
title: Roadmaps Index
status: active
owner: @codex
priority: medium
complexity: 2
created: 2026-05-25
updated: 2026-05-25
tags: [documentation, roadmaps, standards, checklist, naming]
---

# Roadmaps Index

This folder contains the active roadmap and checklist docs for the repo.
It also sets the naming and checklist structure pattern for future items so new docs stay easy to scan.

## File naming conventions

Use short, descriptive, hyphenated names.

Preferred patterns:
- `portfolio-roadmap.md` for the top-level portfolio tracker
- `README.md` for the roadmap package index or overview
- `phases.md` for phase-based milestone tracking
- `*-checklist.md` for ordered parity or closure checklists
- `*-runbook.md` for operator or publish workflows
- `*-matrix.md` for inventory-style route or surface maps
- `*-notes.md` or `release-notes.md` for migration or release notes

Keep names tied to the doc’s actual purpose. Avoid vague labels like `plan.md` or `notes.md` when the doc is really a checklist, runbook, or matrix.

## Recommended section structure

### Roadmap package README

Use this order when a roadmap has an overview page:

1. Summary
2. Current Status
3. Goals
4. Core Idea or Architecture
5. Proposed Solution Shape
6. Docs or Related Links
7. Initial Scope
8. Non-Goals

### Checklist docs

Use this order for working checklists:

1. Purpose
2. Scope
3. Discovery
4. Implementation or Provider Flow
5. Validation
6. Exit Criteria or Follow-Up
7. Notes or Latest Validation when needed

### Runbook docs

Use this order for operator workflows:

1. When to use
2. Publish or operational flow
3. Routine operator checks
4. Backup
5. Restore
6. Release-ready checklist
7. Notes

## Checklist item style

- Keep checklist items short and action-oriented.
- Use one item per observable outcome.
- Mark items complete only when the behavior or verification is real.
- Keep section labels stable so future slices can be compared easily.
- Prefer a small number of named sections over large prose blocks.

## Templates

- [Cross-Repo Checklist Template](./cross-repo-checklist-template.md)

## Roadmap layers

- [Feature-Level Roadmaps](./features/README.md)
- [Initiative-Level Roadmaps](./initiatives/README.md)

## Current roadmap surfaces

- [Portfolio Roadmap](./portfolio-roadmap.md)
- [C# Blazor Multisite Rewrite](./csharp-blazor-multisite/README.md)
