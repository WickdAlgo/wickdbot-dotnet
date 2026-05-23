# Repository Guidelines

## Project Structure & Module Organization

- `PLAN.md` is the current architecture and strategy plan.
- `docs/implementation-history/` contains phase records such as `phase-0.md` and `phase-1.md`.
- Use `docs/implementation-history/phase-x-template.md` when creating or reshaping phase records. Keep the structure simple: Goal, Planned Scope, Completed Work, Validation, and Notes.
- `src/Wickd.Core/` contains reusable models, data contracts, engines, backtesting, and infrastructure.
- `src/Wickd.Adapters.Ccxt/` contains the CCXT-backed market data adapter package.
- `src/Wickd.Cli/` contains the .NET 8 CLI/global tool and packaged default configuration files.
- `tests/Wickd.Core.Tests/`, `tests/Wickd.Adapters.Ccxt.Tests/`, and `tests/Wickd.Cli.Tests/` contain xUnit tests.
- `tests/Wickd.TestSupport/` contains shared test fakes and utilities.
- `.agents/skills/wickdbot-dotnet-engineering/` contains repository-specific engineering guidance for agent work.
- `.agents/skills/wickdbot-commit-message/` contains the repository commit message convention for agent-generated commit subjects and bodies.
- `.agents/skills/implementation-history/` contains the workflow for updating phase records in `docs/implementation-history/`.

Expected Core module folders under `src/Wickd.Core/` include `Models/`, `Data/`, `Engines/`, `Backtesting/`, `Analysis/`, and `Infrastructure/`.

## Build, Test, and Development Commands

Run commands from the repository root:

```text
dotnet restore Wickd.slnx
dotnet build Wickd.slnx
dotnet test Wickd.slnx
dotnet run --project src/Wickd.Cli -- --help
```

- `restore` downloads NuGet dependencies.
- `build` compiles the app and tests.
- `test` runs the xUnit test suite.
- `run` starts the CLI locally.

## DocFX Documentation

DocFX turns C# XML documentation comments from `src/Wickd.Core/` and `src/Wickd.Adapters.Ccxt/` into a local browsable documentation site. The local tool manifest lives in `.config/dotnet-tools.json`; do not require a global DocFX install.

Run commands from the repository root:

```text
dotnet tool restore
dotnet docfx metadata docs/docfx.json
dotnet docfx build docs/docfx.json
dotnet docfx serve docs/_site --port 8080
```

- `metadata` reads `src/Wickd.Core/Wickd.Core.csproj` and `src/Wickd.Adapters.Ccxt/Wickd.Adapters.Ccxt.csproj`, then generates API metadata under `docs/api/`.
- `build` creates the static site under `docs/_site/`.
- `serve` previews the generated site locally.
- Keep curated docs files such as `docs/index.md`, `docs/toc.yml`, `docs/docfx.json`, `docs/filterConfig.yml`, and `docs/api/index.md` in source control.
- Do not commit generated DocFX outputs such as `docs/_site/`, `docs/api/*.yml`, or `docs/api/.manifest`.

## Coding Style & Naming Conventions

Use C# 12/.NET 8 patterns. Keep implicit usings enabled. Prefer records for immutable contracts and classes for behavior-rich services. Use explicit Wickd domain names instead of generic names like `Manager`, `Helper`, or `Processor`.

Use 4-space indentation. Public and internal contract types should have useful XML documentation. Keep comments focused on non-obvious trading, backtest, or algorithm decisions.

## Testing Guidelines

Tests use xUnit with `Microsoft.NET.Test.Sdk` and `coverlet.collector`. Add focused behavior tests for new contracts, command validation, normalization, and deterministic trading rules. Test names should describe the behavior, for example `ParseRejectsUnsupportedTimeframe`.

Run:

```text
dotnet test Wickd.slnx
```

## Commit & Pull Request Guidelines

Use Conventional Commits for new commit messages so Codex, Copilot, and humans all produce the same shape:

```text
<type>(<scope>): <imperative summary>
```

- Keep the subject line lower-case after the type, imperative, specific, under 72 characters, and without a trailing period.
- Prefer a scope for non-trivial changes. Common scopes are `cli`, `config`, `data`, `structures`, `backtest`, `analysis`, `docs`, `docfx`, `tests`, `ci`, and `skills`.
- Use these types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `chore`, and `revert`.
- Add a blank line and a short bullet body only when the commit spans multiple important changes or needs context.
- Mark breaking changes with `!` after the scope or type and include a `BREAKING CHANGE:` footer.
- Do not use vague subjects such as `update stuff`, duplicate phrasing such as `fix: fix ...`, or bundle unrelated work.

Examples:

```text
feat(structures): journal staged liquidity lifecycle
fix(config): load local appsettings overrides safely
docs(docfx): document local API site workflow
test(structures): cover candidate-to-breakout transitions
chore(skills): add backtest review analyzer skill
```

Pull requests should include a short summary, validation commands run, and links to relevant plan or phase documents. Include screenshots only for UI or chart fixture changes.

## Security & Configuration Tips

Do not commit secrets. Public defaults live in `src/Wickd.Cli/appsettings.defaults.json` and `src/Wickd.Cli/markets.defaults.json`; installed users edit copied files under their user configuration directory. `.env` is not part of the MVP. Keep generated runtime data such as `data/cache/` and `runs/` out of commits unless a fixture explicitly requires it.
