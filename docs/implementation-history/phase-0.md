# Phase 0: .NET 8 Project Foundation

Date: 2026-05-09

## Goal

Before module work starts, migrate the project from classic .NET Framework 4.7.2 to SDK-style .NET 8.

## Planned Scope

- Use C# 12 and .NET 8.
- Use one main console app project for the MVP.
- Add one separate test project.
- Use internal folders and namespaces for boundaries instead of separate class library projects.
- Add `System.CommandLine` for CLI subcommands.
- Replace `App.config` and old assembly metadata with modern .NET configuration.

## Completed Work

- Migrated the main app to an SDK-style .NET 8 project under `src/WickdBot`.
- Added a separate .NET 8 test project under `tests/WickdBot.Tests`.
- Added `System.CommandLine`.
- Added command skeletons for:
  - `fetch`
  - `backtest`
  - `analyze`
- Added modern configuration files:
  - `appsettings.json`
  - `markets.json`
- Moved assembly metadata into the SDK-style project file.
- Removed old .NET Framework artifacts:
  - `App.config`
  - `AssemblyInfo.cs`
  - classic `TargetFrameworkVersion` project metadata
- Added `InternalsVisibleTo` so tests can verify internal app wiring without making production types public.

## Validation

Phase 0 was verified with:

```text
dotnet test WickdBot.slnx
```

Result:

```text
Passed: 4
Failed: 0
Skipped: 0
```

## Notes

The project intentionally remains a single console app for the MVP. Separate class library projects can be introduced later only if module boundaries need to become physical assembly boundaries.
