# WickdAlgo

WickdAlgo is a C#/.NET 8 trading research platform for deterministic backtest
plumbing and Smart Money Concepts structure journaling. Its technical codebase
uses the `Wickd.*` project, package, and namespace family.

The current implementation supports historical candle fetching/caching and
backtest replay through the `A1 -> B` path:

```text
A1 HistoricalDataSource -> B StructureEngine -> structures.jsonl
```

It does not yet perform setup selection, trade creation, settlement, live
execution, dashboard rendering, or AI analyst reasoning.

## Current Status

Implemented:

- `fetch`: fetches and caches completed historical OHLCV candles for one market
  and one timeframe.
- Dataset aliases: `fetch --alias` saves a reusable range in
  `data/datasets.json`; `backtest --dataset` replays it.
- `backtest`: replays cached candles, runs the structure engine, and writes
  `runs/{runId}/structures.jsonl`.
- `manage`: lists and deletes saved dataset aliases, optional cached candle
  files, and local backtest run output folders.
- Structure journaling: emits deterministic events for swings, equal highs/lows,
  liquidity breaches, sweeps, order blocks, expansion/FVG events, and lifecycle
  updates.

Placeholder:

- `analyze`: parses `--run-id`, then returns a not-implemented message.

Not implemented yet:

- Setup engine.
- Trade engine.
- Backtest settlement outcomes.
- Live execution.
- AI analyst agent.
- Web or TradingView dashboard.

## Requirements

- .NET 8 SDK.
- Network access for first-time NuGet restore.
- Network access when fetching public historical candles.

The current production market data path supports Binance USD-M futures through
CCXT. Markets configured for unsupported exchange IDs are present for future
work but will fail historical fetch until a matching data client exists.

## Quick Start

Run commands from the repository root:

```text
dotnet restore Wickd.slnx
dotnet build Wickd.slnx
dotnet test Wickd.slnx
dotnet run --project src/Wickd.Cli -- --help
```

Package and install the CLI as a local NuGet tool for a smoke test:

```text
dotnet pack src/Wickd.Cli/Wickd.Cli.csproj -c Release
dotnet tool install Wickd.Cli --source src/Wickd.Cli/bin/Release --tool-path artifacts/wickd-tool
artifacts/wickd-tool/wickd --help
```

After publishing to NuGet, users install and run the WickdAlgo CLI as a .NET tool:

```text
dotnet tool install --global Wickd.Cli
wickd config init
wickd config path
wickd --help
```

Fetch and name a reusable historical candle range:

```text
dotnet run --project src/Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --alias may6-session --force
```

Replay that dataset through the current structure engine:

```text
dotnet run --project src/Wickd.Cli -- backtest --dataset may6-session --run-id phase-3-smoke
```

List or clean generated local artifacts:

```text
dotnet run --project src/Wickd.Cli -- manage datasets list
dotnet run --project src/Wickd.Cli -- manage datasets delete --alias may6-session --delete-cache
dotnet run --project src/Wickd.Cli -- manage runs list
dotnet run --project src/Wickd.Cli -- manage runs delete --run-id phase-3-smoke --force
```

The backtest command currently stops after writing structure events. It does not
create `setups.jsonl`, `trades.jsonl`, `outcomes.jsonl`, or `analysis.json`.

## Generated Outputs

The WickdAlgo CLI writes generated runtime data under ignored local folders:

```text
data/cache/.../candles.jsonl
data/datasets.json
runs/{runId}/structures.jsonl
```

- `candles.jsonl` contains normalized historical candle input.
- `data/datasets.json` maps friendly aliases to fetched ranges.
- `structures.jsonl` contains deterministic structure events for one backtest
  run.

## Project Structure

```text
Wickd/
  PLAN.md
  README.md
  src/
    Wickd.Core/
      Backtesting/
      Data/
      Engines/
      Infrastructure/
      Models/
    Wickd.Adapters.Ccxt/
      CcxtBinanceMarketDataClient.cs
    Wickd.Cli/
      Program.cs
      appsettings.defaults.json
      markets.defaults.json
  tests/
    Wickd.Core.Tests/
    Wickd.Adapters.Ccxt.Tests/
    Wickd.Cli.Tests/
    Wickd.TestSupport/
  docs/
    implementation-history/
```

- `src/Wickd.Core/` contains reusable engine, data, model, backtesting, and
  infrastructure contracts.
- `src/Wickd.Adapters.Ccxt/` contains the CCXT-backed Binance market data
  adapter.
- `src/Wickd.Cli/` contains the .NET 8 CLI/global tool entry point and
  packaged configuration defaults.
- `tests/` contains split xUnit coverage for Core, adapter, CLI, and shared
  test support.
- `docs/implementation-history/` records completed implementation phases.
- `PLAN.md` is the current architecture and strategy plan.

## Configuration

Packaged defaults live beside the CLI project:

```text
src/Wickd.Cli/appsettings.defaults.json
src/Wickd.Cli/markets.defaults.json
```

- `appsettings.defaults.json` contains public reference defaults for storage,
  backtest, setup, trade, settlement, and structure settings.
- `markets.defaults.json` maps Wickd market IDs, such as `BTC_USDT_PERP`, to
  exchange IDs and exchange symbols.
- `appsettings.Local.json` remains ignored by Git for private repository-local
  tuning during development, but it is not packaged.

Installed users should edit user-owned copies, not package files. Initialize
them with:

```text
wickd config init
wickd config path
```

Default user config locations:

```text
Windows: %APPDATA%\Wickd\
macOS: ~/Library/Application Support/Wickd/
Linux: $XDG_CONFIG_HOME/wickd/ or ~/.config/wickd/
```

Configuration path precedence:

```text
--config <path>
WICKD_CONFIG
user-profile appsettings.json
```

Each backtest run is for exactly one market and one timeframe.

## Documentation

DocFX is configured through the repository local .NET tool manifest. Use it to
build browsable API documentation from C# XML comments:

```text
dotnet tool restore
dotnet docfx metadata docs/docfx.json
dotnet docfx build docs/docfx.json
dotnet docfx serve docs/_site --port 8080
```

Generated DocFX output under `docs/_site/` and generated API YAML files are not
source files.

## More Context

- [PLAN.md](PLAN.md) describes the current architecture and strategy direction.
- [docs/implementation-history](docs/implementation-history) records completed
  implementation phases and validation notes.
