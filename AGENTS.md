# Repository Guidelines

## Project Structure & Module Organization

- `PLAN.md` is the current architecture and strategy plan.
- `docs/implementation-history/` contains phase records such as `phase-0.md` and `phase-1.md`.
- Use `docs/implementation-history/phase-x-template.md` when creating or reshaping phase records. Keep the structure simple: Goal, Planned Scope, Completed Work, Validation, and Notes.
- `src/WickdBot/` contains the .NET 8 CLI app, configuration files, and future module folders.
- `tests/WickdBot.Tests/` contains xUnit tests.
- `.agents/skills/wickdbot-dotnet-engineering/` contains repository-specific engineering guidance for agent work.

Expected module folders under `src/WickdBot/` include `Models/`, `Data/`, `Engines/`, `Backtesting/`, `Analysis/`, and `Infrastructure/`.

## Build, Test, and Development Commands

Run commands from the repository root:

```text
dotnet restore WickdBot.slnx
dotnet build WickdBot.slnx
dotnet test WickdBot.slnx
dotnet run --project src/WickdBot -- --help
```

- `restore` downloads NuGet dependencies.
- `build` compiles the app and tests.
- `test` runs the xUnit test suite.
- `run` starts the CLI locally.

## DocFX Documentation

DocFX turns C# XML documentation comments from `src/WickdBot/` into a local browsable documentation site. The local tool manifest lives in `.config/dotnet-tools.json`; do not require a global DocFX install.

Run commands from the repository root:

```text
dotnet tool restore
dotnet docfx metadata docs/docfx.json
dotnet docfx build docs/docfx.json
dotnet docfx serve docs/_site --port 8080
```

- `metadata` reads `src/WickdBot/WickdBot.csproj` and generates API metadata under `docs/api/`.
- `build` creates the static site under `docs/_site/`.
- `serve` previews the generated site locally.
- Keep curated docs files such as `docs/index.md`, `docs/toc.yml`, `docs/docfx.json`, `docs/filterConfig.yml`, and `docs/api/index.md` in source control.
- Do not commit generated DocFX outputs such as `docs/_site/`, `docs/api/*.yml`, or `docs/api/.manifest`.

## Coding Style & Naming Conventions

Use C# 12/.NET 8 patterns. Keep implicit usings enabled. Prefer records for immutable contracts and classes for behavior-rich services. Use explicit WickdBot domain names instead of generic names like `Manager`, `Helper`, or `Processor`.

Use 4-space indentation. Public and internal contract types should have useful XML documentation. Keep comments focused on non-obvious trading, backtest, or algorithm decisions.

## Testing Guidelines

Tests use xUnit with `Microsoft.NET.Test.Sdk` and `coverlet.collector`. Add focused behavior tests for new contracts, command validation, normalization, and deterministic trading rules. Test names should describe the behavior, for example `ParseRejectsUnsupportedTimeframe`.

Run:

```text
dotnet test WickdBot.slnx
```

## Commit & Pull Request Guidelines

Recent commits use concise imperative messages such as `Add CLI command skeleton` and `Move assembly metadata into project file`. Follow that style: start with a verb, keep the subject specific, and avoid bundling unrelated changes.

Pull requests should include a short summary, validation commands run, and links to relevant plan or phase documents. Include screenshots only for UI or chart fixture changes.

## Security & Configuration Tips

Do not commit secrets. MVP configuration lives in `src/WickdBot/appsettings.json` and `src/WickdBot/markets.json`; `.env` is not part of the MVP. Keep generated runtime data such as `data/cache/` and `runs/` out of commits unless a fixture explicitly requires it.
