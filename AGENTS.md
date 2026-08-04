# AGENTS.md

## Purpose

This file is the repo-local guide for coding agents working in this repository.
Follow the closest `AGENTS.md` first, then the workspace-root `AGENTS.md` when this repo lives under `~/code`.

## Default Workflow

- Start with understanding first, then planning, then implementation.
- Read the repository `README.md` and any local docs before changing code.
- Check `docs/features/` and `docs/roadmaps/` when they exist.
- Prefer small, traceable changes over broad rewrites.
- Ask a direct question if the work is ambiguous or would require a risky assumption.
- Preserve existing patterns unless there is a strong reason to change them.
- If this repo participates in portfolio monitoring, treat the Dev Forge Work Matrix as the operator-facing control surface; keep repo-local current-work notes short and avoid duplicating portfolio state in multiple places.

## Validation

- Prefer creating or updating unit tests when they are the right validation surface.
- Use repo-native build, test, and lint commands instead of guessing.
- Verify the smallest relevant surface that proves the change.
- If a change affects docs, update the docs and checklists in the same pass.

## Code Standards

- Keep edits focused and reviewable.
- Prefer strongly typed code and explicit boundaries where practical.
- Avoid destructive commands unless explicitly requested.
- Do not revert user changes you did not make.
- Keep secrets and tokens out of source control.

## Docs Standards

- Treat documentation as part of the product.
- Prefer checklist-heavy docs for progress, validation, and follow-up work.
- Record implementation notes when the repo has ongoing feature work.
- When behavior changes, update the relevant feature or roadmap doc before finishing.

## Git Hygiene

- Do not rewrite shared history unless explicitly asked.
- Do not force-push by default.
- Keep the working tree clean enough that future diffs are easy to inspect.

## Repo Notes

- Add repo-specific commands, constraints, and validation steps below this line.

<!-- dev-forge:low-interruption:start version=1 -->
## Low-Interruption Execution

- Treat explicit outcome requests such as "fix," "build," "complete," and
  "finish" as continuing authorization for bounded work toward that outcome.
- Continue through diagnosis, implementation, tests, commits, pushes, review
  feedback, and CI repair without renewed confirmation.
- New defects discovered within the same task or pull request remain in scope
  when the repair is reversible, clearly supported, and consistent with the
  existing architecture.
- Progress updates are informational and do not pause execution.
- Do not request confirmation when the only realistic alternatives are the
  clearly supported action and inaction.
- Use a blocking checkpoint only at a genuine impasse. Present two or three
  materially different choices as **A**, **B**, and optionally **C**; recommend
  one and ask for a one-letter reply.
- Do not use "Done — continue" as a generic permission gate.
- Preserve explicit approval boundaries for merge, deploy, destructive work,
  secret or access changes, material cost, external communication, and credible
  downtime or data-loss risk.
<!-- dev-forge:low-interruption:end -->

<!-- dev-forge:dotnet-mvvm-testing:start version=1 -->
## .NET MVVM and Testing Standard

- Keep Views (XAML, Blazor components) limited to layout and bindings; no
  business logic in code-behind or `@code` blocks beyond simple UI wiring.
- ViewModels are plain C# or `ReactiveObject`/`ObservableObject`-based,
  constructed via DI, free of direct UI-framework dependencies, and injected
  with services as interfaces so they can be unit-tested without a UI thread.
- Prefer Blazor over introducing a second UI stack for new .NET UI surfaces —
  its component model maps onto the same MVVM split used elsewhere in the
  portfolio. Record the reason in this repo's `AGENTS.md` when a different
  stack is a deliberate exception.
- Unit-test ViewModels, parsers, and business/domain logic; keep those tests
  free of filesystem, network, database, or UI-thread dependencies.
- Add integration tests where a change crosses a boundary a unit test can't
  observe (parser-to-event pipelines, storage round trips, external API
  calls).
- Full standard: `docs/architecture/dotnet-mvvm-and-testing-standard.md` in
  `dev-forge`.
<!-- dev-forge:dotnet-mvvm-testing:end -->
