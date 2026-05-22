# Wickd Technical Rename and Package Split

Date: 2026-05-22

## Goal

Rename the technical codebase from `WickdBot.*` to `Wickd.*` while keeping `WickdAlgo` as the product and marketing umbrella. Complete the package split before Phase 4 setup-engine work expands the public contract surface.

## Planned Scope

- Keep a single Git repository.
- Rename the solution to `Wickd.slnx`.
- Split source code into independently packable projects:
  - `src/Wickd.Core`
  - `src/Wickd.Adapters.Ccxt`
  - `src/Wickd.Cli`
- Split tests into matching projects:
  - `tests/Wickd.Core.Tests`
  - `tests/Wickd.Adapters.Ccxt.Tests`
  - `tests/Wickd.Cli.Tests`
  - `tests/Wickd.TestSupport`
- Rename namespaces, assemblies, package IDs, project references, and friend assemblies from `WickdBot.*` to `Wickd.*`.
- Rename public technical types from `WickdBot...` to `Wickd...`.
- Keep `WickdAlgo` for product-facing copy and package descriptions.
- Change the tool command from `wickdbot` to `wickd`.
- Change configuration identity to:
  - environment variable `WICKD_CONFIG`
  - JSON root `Wickd`
  - Windows/macOS config folder `Wickd`
  - Linux config folder `wickd`
- Update README, AGENTS guidance, DocFX metadata, and validation commands.

## Completed Work

- Renamed the solution from `WickdBot.slnx` to `Wickd.slnx`.
- Created the source package layout:
  - `Wickd.Core` for reusable models, data contracts, engines, backtesting, and infrastructure.
  - `Wickd.Adapters.Ccxt` for `CcxtBinanceMarketDataClient`.
  - `Wickd.Cli` for command-line wiring and global-tool packaging.
- Moved tests into separate projects for core, CLI, CCXT adapter behavior, and shared test support.
- Updated package identities:
  - `Wickd.Core`
  - `Wickd.Adapters.Ccxt`
  - `Wickd.Cli`
- Updated the CLI tool command to `wickd`.
- Renamed namespaces and technical types from `WickdBot` to `Wickd`, including:
  - `WickdSettings`
  - `WickdConfigurationLoader`
  - `WickdConfigurationPaths`
  - `WickdConfigurationException`
  - `WickdDataException`
- Updated `InternalsVisibleTo` declarations for the renamed CLI and test assemblies.
- Updated configuration defaults and tests to use `WICKD_CONFIG` and the `Wickd` JSON root.
- Updated user configuration path resolution to use lowercase `wickd` for Linux config directories.
- Updated README and DocFX metadata to use `WickdAlgo` for product-facing docs and `Wickd.*` for technical package names.
- Updated existing implementation-history validation examples so future commands target `Wickd.slnx` and `src/Wickd.Cli`.

## Validation

The rename and package split were validated with:

```text
dotnet restore Wickd.slnx
dotnet build Wickd.slnx
dotnet test Wickd.slnx
dotnet run --project src/Wickd.Cli -- --help
dotnet pack src/Wickd.Core -c Release
dotnet pack src/Wickd.Adapters.Ccxt -c Release
dotnet pack src/Wickd.Cli -c Release
dotnet tool install Wickd.Cli --source src/Wickd.Cli/bin/Release --tool-path artifacts/wickd-tool
artifacts\wickd-tool\wickd --help
```

Results:

- Restore completed successfully.
- Build completed successfully.
- Tests passed:
  - `Wickd.Adapters.Ccxt.Tests`: 8 passed.
  - `Wickd.Core.Tests`: 71 passed.
  - `Wickd.Cli.Tests`: 43 passed.
- Release packages were produced for `Wickd.Core`, `Wickd.Adapters.Ccxt`, and `Wickd.Cli`.
- Local tool installation succeeded and exposed the `wickd` command.
- Validation emitted `NU1900` warnings because NuGet vulnerability metadata could not be loaded from `https://api.nuget.org/v3/index.json`; the commands still completed successfully.

## Notes

- This is a breaking rename. No compatibility shim was added for old `WickdBot` namespaces, package IDs, environment variables, JSON roots, or user configuration folders.
- The physical repository folder may remain `WickdBot`; the technical solution and package names are now `Wickd.*`.
- The help usage line currently displays `Wickd.Cli` because System.CommandLine derives the executable display name from the assembly context, but the installed tool command is `wickd`.
- Existing repo-local agent skill folders still use their historical `wickdbot-*` directory names because those are filesystem tool names, not product assemblies or NuGet packages.
