---
title: Cross-Repo Checklist Template
status: active
owner: @codex
priority: medium
complexity: 1
created: 2026-05-25
updated: 2026-05-25
tags: [documentation, template, checklist, roadmap, cross-repo]
---

# Cross-Repo Checklist Template

Use this as a starting point when a change spans more than one repo or needs a reusable checklist shape for future work.

## Header

- **Item name:**
- **Owning repo:**
- **Dependent repo(s):**
- **Status:**
- **Last updated:**
- **Related docs:**

## Scope

Describe the smallest useful slice.

- What is in scope:
- What is out of scope:
- Why this slice exists:
- What would count as done:

## Repo boundary

| Repo | Role | Files or surfaces | Notes |
| --- | --- | --- | --- |
| owning repo | source of truth for the implementation | | |
| dependent repo | downstream consumer or mirror | | |

## Discovery

- [ ] Read the owning repo docs
- [ ] Read the dependent repo docs
- [ ] Confirm the live files or surfaces that will change
- [ ] Confirm the validation command(s)

## Implementation

- [ ] Make the smallest implementation change
- [ ] Keep naming and section order consistent
- [ ] Update the dependent repo docs if the contract changed
- [ ] Preserve unrelated worktree noise

## Validation

- [ ] Run the smallest meaningful test or smoke
- [ ] Run the repo-native gate if the change touches shared wiring
- [ ] Verify the docs match the live state

## Follow-up

- [ ] Record what remains for the next slice
- [ ] Capture any repo-specific quirks or constraints
- [ ] Link any new runbook, matrix, or checklist that should stay in sync
