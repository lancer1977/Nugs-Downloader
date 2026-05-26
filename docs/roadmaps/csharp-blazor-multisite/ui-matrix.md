---
title: UI Matrix
status: active
owner: @codex
priority: medium
complexity: 2
created: 2026-05-25
updated: 2026-05-25
tags: [roadmap, ui, routes, navigation, dashboard, csharp, blazor]
---

# UI Matrix

This page captures the live route, menu, and dashboard surface for the C# Blazor host.
It is meant to stay aligned with the rendered host, not with the old Go/React tree.

## Route Matrix

| Route | Page | Purpose | Primary data | Coverage |
| --- | --- | --- | --- | --- |
| `/` | Home / Dashboard | Overview of providers, jobs, recent activity, and file state | provider catalog, job repo, file-state repo | `ComponentTests.HomePage_RendersDashboardAndSummaries`, `WebIntegrationTests.WebHost_RendersPrimaryPages` |
| `/queue` | Queue | Queue a new download and pick output defaults | provider catalog, credential store, workflow, storage options | `ComponentTests.QueuePage_SubmitFlow_CallsWorkflowAndShowsResult`, `WebIntegrationTests.WebHost_RendersPrimaryPages` |
| `/login` | Login | Capture provider credentials and save a provider account | provider catalog, credential store, secret vault | `ComponentTests.LoginPage_ShowsHintsAndAuthResult`, `WebIntegrationTests.WebHost_RendersPrimaryPages` |
| `/file-state` | File State | Inspect file-state persistence and disk status | file-state repo, job repo | `ComponentTests.FileStatePage_RendersAllJobState`, `WebIntegrationTests.WebHost_RendersPrimaryPages` |
| `/provider-settings` | Provider Settings | Review provider capabilities and stored accounts | provider catalog, credential store | `ComponentTests.ProviderSettingsPage_RendersCapabilityMatrix`, `WebIntegrationTests.WebHost_RendersPrimaryPages` |
| `/jobs/{jobId}` | Job Details | Inspect a single job, discovery result, file state, resume, and retry actions | job repo, file-state repo, workflow | `ComponentTests.JobDetailsPage_RendersJobAndFiles`, `ComponentTests.JobDetailsPage_ResumeAndRetryActionsCallWorkflow`, `WebIntegrationTests.WebHost_RendersJobDetailsFromRegisteredRepositories` |

## Menu Matrix

| Label | Target | Source | Notes |
| --- | --- | --- | --- |
| Dashboard | `/` | `MainLayout` app bar | top-level dashboard entry |
| Queue | `/queue` | `MainLayout` app bar | primary queueing flow |
| Login | `/login` | `MainLayout` app bar | provider auth flow |
| Files | `/file-state` | `MainLayout` app bar | file-state inspection |
| Providers | `/provider-settings` | `MainLayout` app bar | provider capability matrix |

## Dashboard Card Matrix

| Section | Backing data | What the card shows | Coverage |
| --- | --- | --- | --- |
| Providers | provider catalog | provider name, id, and capability summary | `ComponentTests.HomePage_RendersDashboardAndSummaries` |
| Jobs | job repository | latest queued/running/complete jobs and links to job details | `ComponentTests.HomePage_RendersDashboardAndSummaries` |
| Recent Activity | job repository | the most recently touched jobs and their status/timestamps | `ComponentTests.HomePage_RendersDashboardAndSummaries` |
| File State Snapshot | file-state repository | current file paths, status, and kind for the most recent job | `ComponentTests.HomePage_RendersDashboardAndSummaries` |

## Validation Notes

- Route coverage is already exercised by the integration tests in `WebIntegrationTests`.
- Dashboard card coverage is exercised by the component tests in `ComponentTests`.
- This matrix should be updated whenever the app shell or dashboard cards change.
