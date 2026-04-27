---
title: Risks
status: active
owner: @codex
priority: high
complexity: 4
created: 2026-04-26
updated: 2026-04-26
tags: [roadmap, risks, csharp, blazor, multisite]
---

# Risks

## Technical Risks

- Site auth flows may differ enough that provider abstraction becomes leaky.
- Resume logic and file-state persistence may need more careful design than the current Go app.
- Browser-based UI credential capture raises storage and security requirements.
- Live site changes may require frequent provider updates.

## Migration Risks

- A big-bang rewrite would likely lose feature parity and slow down feedback.
- Porting too much before the first provider works end to end will create churn.
- If the file-state model is not designed early, downloads and resume behavior will be difficult to reconcile later.

## Operational Risks

- Credential handling must be encrypted or OS-backed where possible.
- The app should avoid logging secrets, URLs with sensitive params, or raw auth payloads.
- Multi-site support can become a maintenance burden without clear provider boundaries.

