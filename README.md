# WickdBot

WickdBot is a C#/.NET 8, CLI-first trading research tool for deterministic
backtest plumbing and Smart Money Concepts structure journaling.

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
dotnet restore WickdBot.slnx
dotnet build WickdBot.slnx
dotnet test WickdBot.slnx
dotnet run --project src/WickdBot -- --help
```

Fetch and name a reusable historical candle range:

```text
dotnet run --project src/WickdBot -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --alias may6-session --force
```

Replay that dataset through the current structure engine:

```text
dotnet run --project src/WickdBot -- backtest --dataset may6-session --run-id phase-3-smoke
```

The backtest command currently stops after writing structure events. It does not
create `setups.jsonl`, `trades.jsonl`, `outcomes.jsonl`, or `analysis.json`.

## Generated Outputs

WickdBot writes generated runtime data under ignored local folders:

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
WickdBot/
  PLAN.md
  README.md
  src/
    WickdBot/
      Program.cs
      appsettings.json
      markets.json
      Backtesting/
      Data/
      Engines/
      Infrastructure/
      Models/
  tests/
    WickdBot.Tests/
  docs/
    implementation-history/
```

- `src/WickdBot/` contains the .NET 8 CLI app and current runtime modules.
- `tests/WickdBot.Tests/` contains xUnit coverage for CLI wiring,
  configuration, data normalization, cache/replay behavior, and structure
  events.
- `docs/implementation-history/` records completed implementation phases.
- `PLAN.md` is the current architecture and strategy plan.

## Configuration

Runtime configuration lives beside the CLI project:

```text
src/WickdBot/appsettings.json
src/WickdBot/markets.json
src/WickdBot/appsettings.Local.json
```

- `appsettings.json` contains committed defaults for storage, backtest, setup,
  trade, settlement, and structure settings.
- `markets.json` maps WickdBot market IDs, such as `BTC_USDT_PERP`, to exchange
  IDs and exchange symbols.
- `appsettings.Local.json` is optional and ignored by Git. Use it for private
  local overrides without changing committed defaults.

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
