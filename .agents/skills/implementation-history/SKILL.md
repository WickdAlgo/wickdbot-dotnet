---
name: implementation-history
description: Record meaningful WickdBot implementation work in docs/implementation-history using the repo's phase record format. Use after implementing a feature, refactor, migration, integration, workflow, or fix that future agents need to understand.
---

# WickdBot Implementation History

Use this skill after meaningful planned work in WickdBot. Do not create entries for typo-only edits, transient experiments, generated output, or changes that do not affect repository behavior, architecture, validation, or workflow.

## Workflow

1. Inspect `docs/implementation-history/` before writing so the new record fits the current phase sequence.
2. Use `docs/implementation-history/phase-x-template.md` when creating a new phase record or reshaping an incomplete one.
3. Prefer updating the active phase record when the work belongs to that phase. Create a new phase record only when the work starts a distinct planned phase.
4. Keep entries factual and concise. Record what changed, what was intentionally left out, and how the work was validated.
5. Mention each validation command that was run and whether it passed, failed, or was skipped.
6. Do not commit generated runtime data such as `data/cache/`, `runs/`, `docs/_site/`, `docs/api/*.yml`, or `docs/api/.manifest` just to support a history entry.

## Entry Shape

Use this structure for phase records:

```text
# Phase X: Short Phase Title

Date: YYYY-MM-DD

## Goal

## Planned Scope

## Completed Work

## Validation

## Notes
```

## WickdBot Conventions

- Keep phase records under `docs/implementation-history/`.
- Use the simple section names already used by the repo: `Goal`, `Planned Scope`, `Completed Work`, `Validation`, and `Notes`.
- Prefer concrete paths such as `src/WickdBot/`, `tests/WickdBot.Tests/`, and `docs/implementation-history/` over generic descriptions.
- Keep open-source and private-strategy boundaries explicit when documenting trading logic, settings, or backtest behavior.
- For validation, prefer the repository commands from `AGENTS.md`, especially `dotnet test WickdBot.slnx` for implemented code changes.
