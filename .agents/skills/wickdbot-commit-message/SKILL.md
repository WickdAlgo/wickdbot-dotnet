---
name: wickdbot-commit-message
description: Generate consistent Conventional Commit messages for WickdBot. Use when the user asks for a commit message, commit title, commit subject, commit body, staged-change summary, or asks Codex to commit local WickdBot changes.
---

# WickdBot Commit Message

## Standard

Use Conventional Commits for WickdBot:

```text
<type>(<scope>): <imperative summary>
```

- Use a lower-case type and optional lower-case scope.
- Keep the summary imperative, specific, under 72 characters, and without a trailing period.
- Prefer lower-case wording after the colon, matching Visual Studio Copilot-style conventional subjects.
- Add a blank line plus a concise bullet body only when the change needs context or spans several meaningful parts.
- If there are unrelated changes, recommend splitting commits and provide separate messages.

## Types

Use only these types unless the user explicitly asks otherwise:

- `feat`: user-visible capability, strategy behavior, domain feature, or new workflow
- `fix`: bug fix or corrected behavior
- `docs`: documentation-only changes, including DocFX content and XML docs
- `test`: test-only additions or corrections
- `refactor`: behavior-preserving code restructuring
- `perf`: performance improvement
- `build`: project files, packages, tool manifests, or build system changes
- `ci`: GitHub Actions or deployment workflow changes
- `chore`: repo maintenance, generated policy, housekeeping, or agent skill upkeep
- `revert`: revert a prior commit

## Scopes

Prefer these repo scopes when they fit:

- `cli`
- `config`
- `data`
- `structures`
- `backtest`
- `analysis`
- `docs`
- `docfx`
- `tests`
- `ci`
- `skills`

Omit the scope only for broad repo-wide changes where one scope would be misleading.

## Workflow

1. Inspect `git status --short`.
2. If staged changes exist, base the message on `git diff --cached --stat` and `git diff --cached`. Otherwise use `git diff --stat` and `git diff`.
3. Identify the dominant intent before choosing the type. Prefer the user-facing or behavior-changing type over the largest file count.
4. Choose the narrowest accurate scope.
5. Draft one primary message. Include a body only when it improves the commit.

## Examples

```text
feat(structures): journal staged liquidity lifecycle
fix(config): load local appsettings overrides safely
docs(docfx): document local API site workflow
test(structures): cover candidate-to-breakout transitions
chore(skills): add backtest review analyzer skill
ci: build and test on pull requests
```

For breaking changes:

```text
feat(backtest)!: change outcome journal schema

BREAKING CHANGE: outcome records now group fills by setup id.
```
