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


## Hermes Kanban First

- For any new slice, backlog item, generated card pack, or DreadBreadcrumb follow-up, create or identify the Hermes Kanban card first.
- Repo-local docs (`00_agile/`, `docs/roadmaps/`, `docs/features/`, `CURRENT-WORK.md`) are tracking mirrors: they should link back to the Hermes `t_*` card and may keep local doc-card IDs for grouping, but they are not the execution source of truth.
- When planning broad work, create a Hermes umbrella/planner card before materializing local generated cards. Promote only the next safe slice(s) to ready; leave broad backlogs in triage/blocked until scoped.
- Every local card row or markdown backlog entry should include `Hermes Kanban: t_*` once the live card exists, or `Hermes Kanban: pending promotion` if it is only a parked local breadcrumb.

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

<!-- dev-forge:low-interruption:start version=5 -->
## Low-Interruption Execution

- Treat explicit outcome requests such as "fix," "build," "complete," and
  "finish" as continuing authorization for bounded work toward that outcome.
- Continue through diagnosis, implementation, tests, commits, pushes, review
  feedback, and CI repair without renewed confirmation.
- New defects discovered within the same task or pull request remain in scope
  only when they are P0/P1 or release-blocking, or while the accepted outcome
  is not yet complete. After acceptance criteria pass, P2-and-lower
  non-blocking findings become linked follow-ups unless the owner explicitly
  keeps them in the active slice.
- Progress updates are informational and do not pause execution.
- Do not request confirmation when the only realistic alternatives are the
  clearly supported action and inaction.
- Use MAID (Multiple-Choice Agentic Interface Development) only for a real
  operator fork. Lead with the recommended option, label it, make choices
  mutually exclusive, and describe what each choice causes.
- Use the current host's native picker when it is available in the current
  mode. Never invoke a picker that the current host or mode does not expose.
  A missing picker is not a blocker: at a genuine impasse, present two or three
  concise, lettered **A**/**B**/**C** choices instead and request the letter.
- Scale a checkpoint to what it costs the operator, not to how important the
  decision feels. A native picker is cheap enough for any real fork; a typed
  fallback remains reserved for a genuine impasse. Neither is a reason to ask
  for reassurance, re-ask a settled decision, or ask what the repository can
  answer.
- Do not offer a picker whose options are an action and its absence. Do not use
  "Done — continue" as a generic permission gate.
- A picker selects a path; it never authorizes merge, deploy, destructive work,
  secret or access changes, material cost, external communication, or credible
  downtime or data-loss risk. Use the applicable approval control for those
  protected actions.
- End every handback with selectable, genuine next steps when a real next-step
  fork remains. Use the native picker when available; otherwise use lettered
  choices. If no second genuine option exists, state the actual status plainly
  and stop rather than inventing a placeholder choice.
- This governs the form of a handback, not the frequency of checkpoints. Do not
  interrupt bounded work more often because of it. Do not add selectable
  options to a machine-readable output constrained by an external schema.
<!-- dev-forge:low-interruption:end -->
