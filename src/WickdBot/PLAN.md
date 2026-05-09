# C# Native WickdBot Modular Architecture

## Summary

WickdBot is a C#/.NET 8 native, backtest-first trading research system. The MVP proves the path `A1 -> B -> C -> D -> E1` with normalized OHLCV candle events for exactly one market and one timeframe per run.

B, C and D are market-agnostic: they do not care which exchange or pair produced the candles. They consume ordered OHLCV candles plus metadata and make decisions from candle structure only.

```text
A1 HistoricalDataSource
  -> B StructureEngine
  -> C SetupEngine
  -> D TradeEngine
  -> E1 BacktestSettlementEngine
  -> DeterministicAnalyzer
```

`BacktestPipeline.cs` orchestrates the complete backtest flow in C#.

## Phase 0

Before module work starts, migrate the current project from classic .NET Framework 4.7.2 to SDK-style .NET 8:

- Use C# 12 and .NET 8.
- Use one main console app project for the MVP.
- Add one separate test project.
- Use internal folders/namespaces for boundaries instead of separate class library projects.
- Add `System.CommandLine` for CLI subcommands.
- Replace `App.config`/old assembly metadata with modern .NET configuration.

## Project Shape

```text
WickdBot/
  Program.cs
  appsettings.json
  markets.json
  Models/
  Data/
  Engines/
  Backtesting/
  Analysis/
  Infrastructure/

WickdBot.Tests/
  Fixtures/
    Golden/
```

Separate class library projects can be introduced later if the module boundaries need it.

## Modules

### Data

- `HistoricalDataSource.cs`: A1. Fetches any configured exchange/symbol/timeframe through `IMarketDataClient`, caches normalized candles as JSONL, and replays them as `source=backtest`.
- `IMarketDataClient.cs`: tiny adapter interface for OHLCV fetching.
- `CcxtMarketDataClient.cs`: CCXT-backed implementation hidden behind `IMarketDataClient`.
- `LiveDataSource.cs`: A2 future contract only. It is not executable in the MVP.

### Engines

- `StructureEngine.cs`: B. Detects neutral structures from OHLCV: swings, EQ liquidity, liquidity sweeps, order blocks, expansion candles, FVGs, FVG fill updates, and lifecycle events.
- `SetupEngine.cs`: C. Tracks candidate OBs across candles, applies strategy rules, emits actionable accepted setups before mitigation, and emits terminal rejects.
- `TradeEngine.cs`: D. Converts accepted setups into pending trade decisions using deterministic risk/reward logic.
- `BacktestSettlementEngine.cs`: E1. Settles pending/open trades online as candles replay and writes outcomes.
- `LiveExecution.cs`: E2 future contract only. It is not executable in the MVP.

### Shared

- `Models/`: C# records and enums shared across modules.
- `Settings.cs`: loads and validates `appsettings.json` and `markets.json`.
- `Journal.cs`: JSONL structured journals.
- `AsyncEventBus.cs`: future/live-mode module, not used in the MVP backtest path.

### Analysis

- `DeterministicAnalyzer.cs`: deterministic post-run analysis over journals.
- `BacktestPipeline.cs`: C# orchestrator connecting A1, B, C, D, E1, journals, and analysis.

## Core Runtime Decisions

- One market and one timeframe per run. Multi-market or multi-timeframe runs are explicitly out of scope.
- A1 can fetch any configured market/timeframe, but each backtest run selects exactly one.
- Default backtest market: `BTC_USDT_PERP` on Binance.
- Default timeframe: `5m`.
- Timeframe is stored/journaled as a validated string and parsed to a duration internally.
- Candle timestamps are UTC open time. Close time is derived as `OpenTimeUtc + timeframe`.
- Only completed candles are processed.
- Domain prices, volumes, risk amounts, and journaled numeric trading values use `decimal`.
- `BacktestPipeline` generates one `runId` and propagates it everywhere.
- Entity IDs are deterministic counters scoped by run and type, such as `ob-000001`, `setup-000001`, and `trade-000001`.

## Configuration

- Use `appsettings.json` as the primary configuration mechanism.
- Do not use `.env` in the MVP.
- Store market mappings in separate `markets.json`.
- `appsettings.json` holds defaults and points to `markets.json`.
- `markets.json` maps canonical market IDs to exchange IDs and exchange symbols.
- Timeframe is a run parameter, not part of the market identity.
- Validate config at startup/fetch time:
  - duplicate canonical IDs fail fast;
  - missing exchange IDs/symbols fail fast;
  - unknown requested markets fail fast;
  - invalid timeframes fail fast.

## CLI

Use `System.CommandLine` with explicit arguments:

```text
fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z
backtest --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z
analyze --run-id <runId>
```

## Storage

- JSONL is the only MVP storage format.
- No CSV path in the MVP.
- Parquet is deferred as a future optimization.
- Candle cache is reusable input and is organized by market data identity:

```text
data/cache/binance/BTC_USDT_PERP/5m/2026-05-06_2026-05-07/candles.jsonl
```

- Run journals are run-specific output:

```text
runs/{runId}/structures.jsonl
runs/{runId}/setups.jsonl
runs/{runId}/trades.jsonl
runs/{runId}/outcomes.jsonl
runs/{runId}/run.json
runs/{runId}/analysis.json
```

Every journal record carries `runId`, `marketId`, and `timeframe`.

`run.json` is the immutable effective settings manifest for the run. It is written before the backtest starts and is used to match results with the exact settings that produced them. It includes:

- `runId`;
- created timestamp;
- market ID, exchange ID, exchange symbol, and timeframe;
- requested date range;
- source candle cache path;
- starting equity, fixed risk percent, TP R, and SL buffer;
- all effective B/C/D/E1 strategy settings;
- enabled/disabled optional rules;
- application version and git commit when available;
- hash of the effective run settings.

## Candle Stream Rules

- A1 normalizes, orders, and deduplicates candles before replay.
- B still validates monotonic candle order to protect its assumptions.
- Duplicate or non-increasing timestamps fail fast.
- Gaps are recorded in run metadata/journals and allowed by default.
- Strict gap failure can be added as a config option.

## Liquidity Definitions

- Buy-side liquidity lives above highs/EQ highs, where buy stops rest.
- Sell-side liquidity lives below lows/EQ lows, where sell stops rest.
- Buy stops can include short-position stops and breakout-buy orders.
- Sell stops can include long-position stops and breakout-sell orders.
- EQH/EQL detection uses configurable basis-point tolerance.
- Actual distance between equal levels is journaled.
- Swing detection uses a simple configurable symmetric fractal window in the MVP.
- Adaptive/volatility-based swing logic is future work.

## Structure Rules

### Order Blocks

- Bullish OB candle must have `close < open`.
- Bearish OB candle must have `close > open`.
- Doji/neutral candles are not OBs in the MVP.
- OB zone bounds use the full candle high/low.
- B searches backward for the last qualifying opposite-color OB candle within a configurable window.
- Default OB search-back window: `3` candles.
- OB zones are immutable after discovery.
- OB lifecycle state is tracked separately: active, mitigated, consumed, invalidated/rejected.
- Any wick touch into the OB zone counts as mitigation and consumes the OB.
- A mitigated OB is never traded again.
- One OB can produce at most one setup and one trade.

### Initial Sweep

- The initial liquidity sweep can be performed by either the selected OB candle or the expansion/displacement candle.
- For bullish setups, either candle may sweep sell-side liquidity using its lower wick.
- For bearish setups, either candle may sweep buy-side liquidity using its upper wick.
- Wick breach is enough. A close through the liquidity level is not required.
- Initial sweep source is journaled as an enum-like type:
  - `ObCandleSweep`: the selected OB candle performs the direction-specific initial sweep.
  - `PostObDisplacementSweep`: the candle after the OB / displacement candle performs the direction-specific initial sweep.
- Both initial sweep source types belong to the same OB strategy family.
- Journal all initial sweeps.
- Mark a deterministic primary sweep by most recent/nearest swept liquidity.

### Expansion and FVG

- Displacement confirmation is journaled as an enum-like type:
  - `SingleExpansionFvg`: MVP mode. A qualifying single expansion candle is directly tied to a classic three-candle FVG.
  - `MultiCandleImpulse`: future extension mode. Displacement unfolds across a multi-candle impulse away from the OB.
- MVP implementation supports `SingleExpansionFvg`.
- `MultiCandleImpulse` is tracked through golden samples and future work, but is not MVP-blocking.
- Expansion uses recent average full candle range as the denominator.
- Recent average range uses only prior candles and excludes the expansion candidate.
- Insufficient lookback means no expansion qualification yet.
- Default expansion threshold: body at least `1.5x` recent average candle range.
- Journal extra expansion features, including body/range and body/average-range ratios.
- Expansion candle must close beyond the OB zone in the displacement direction:
  - bullish: close above OB high;
  - bearish: close below OB low.
- Breaking a prior swing high/low is not required in the MVP, but is journaled as a feature.
- The required FVG uses the classic three-candle definition only.
- The expansion candle is the middle candle of the required FVG.
- The OB candle may be candle 1 of the FVG, but the expansion/middle candle must be after the OB.
- Expansion/FVG may appear within a configurable short window after the OB.
- Default expansion/FVG window: `1 or 2` candles after the OB.
- FVG fill percentage uses wick penetration.
- FVG max-fill-before-entry is configurable and disabled by default.
- FVG fill percentage is always journaled.
- The required FVG must remain at least partially open until setup acceptance.
- If the required FVG is filled more than half before setup acceptance, the candidate is terminally rejected and the same OB is not revived by a later FVG.

## Setup Rules

C tracks candidate OB states on every candle. It emits:

- accepted/actionable setup records when all pre-entry conditions are satisfied;
- terminal reject records when a candidate can no longer become valid.

It does not emit noisy "not ready yet" records.

### MVP Rule Set

1. Initial sweep: the selected OB candle or expansion candle must sweep direction-specific prior liquidity.
2. Expansion/FVG: a qualifying displacement expansion and directly tied classic FVG must appear within the configured window.
3. Post-OB liquidity: after displacement, at least one direction-appropriate swing/EQ liquidity level must form between current price and the OB mitigation zone.
4. FVG viability: the required FVG must not be filled more than half before setup acceptance.

FVG max-fill threshold filtering, such as `50%`, is available as config but disabled by default to avoid false negatives.

### Post-OB Liquidity

For a bullish setup:

- the required post-OB liquidity is sell-side liquidity from swing lows/EQLs;
- it must be above the bullish OB zone;
- it must sit between current price and the OB mitigation zone;
- once this liquidity exists, the setup can become actionable before mitigation;
- on the path back to the OB, price necessarily sweeps that liquidity before touching the OB.

For a bearish setup, mirror the rule:

- the required post-OB liquidity is buy-side liquidity from swing highs/EQHs;
- it must be below the bearish OB zone;
- it must sit between current price and the OB mitigation zone;
- once this liquidity exists, the setup can become actionable before mitigation;
- on the path back to the OB, price necessarily sweeps that liquidity before touching the OB.

One or more eligible post-OB liquidity levels can satisfy the rule. Journal all eligible levels and all swept levels.

If price mitigates the OB before all pre-entry conditions are ready, terminally reject as `MitigatedBeforeSetupReady`.

Post-OB liquidity must come from detected swing/EQ liquidity only. Arbitrary local candle highs/lows do not qualify.

## Trade Rules

- D creates a pending limit order immediately when C emits an actionable setup.
- Creating the pending order does not consume the OB.
- OB mitigation/fill happens only on first wick touch of the OB zone.
- Entry is at the OB near edge:
  - bullish entry = OB high;
  - bearish entry = OB low.
- Stop loss is beyond the OB far edge or expansion candle far edge whichever is farer, by a configurable buffer.
- Stop buffer is configurable, default: `0`.
- Take profit is configurable, default: `2.0R`.
- Nearest opposing liquidity target is journaled as a feature but does not set TP in the MVP.
- Each accepted setup/OB can create one trade.
- Multiple trades from different setups may overlap.
- Each trade risk is configurable, default: `1%` of starting equity independently.
- Default starting equity: `10,000 USDC`.
- No fee simulation in backtests.
- No slippage simulation in backtests.
- No leverage or margin modeling in the MVP.
- `TradeIntent` includes deterministic position size:

```text
positionSize = riskAmount / abs(entry - stop)
```

- Invalid or zero risk distance is rejected by D with a stable reason such as `InvalidRiskDistance`.
- `trades.jsonl` stores both accepted and rejected trade decisions with `status`.

### Pending Orders

- Pending order expiry is configurable.
- Default expiry is disabled.
- Without expiry, pending orders last until mitigation/fill or end of run.
- There is no pre-fill far-edge invalidation path for near-edge entries in the MVP.
- If price reaches the far edge while pending, it necessarily crossed the near-edge entry first; settlement then handles fill/outcome ordering.
- Future work: invalidate stale setups when likely target liquidity is already taken before entry.

## Backtest Settlement

- E1 settles trades online as candles replay.
- Each new candle updates all pending/open trades.
- E1 uses OHLC only in the MVP.
- If OHLC cannot prove whether entry, SL, or TP happened first, write `ambiguous_skip`.
- Ambiguous skips are excluded from ML winner/loser labels.
- Ambiguous skips are included in opportunity stats and reports.
- Outcomes include TP, SL, no-fill, open-at-end, canceled/expired, and ambiguous-skip.

## Journaling

Use separate JSONL files by record family:

- `structures.jsonl`
- `setups.jsonl`
- `trades.jsonl`
- `outcomes.jsonl`

`structures.jsonl` includes every detected discrete structure event and lifecycle transition:

- swings;
- EQ liquidity;
- liquidity sweeps;
- OB discoveries;
- OB lifecycle transitions;
- expansion candles;
- FVG discoveries;
- FVG fill updates.

Do not journal full `StructureSnapshot` on every candle. C can use snapshots internally.

Stable terminal reject categories should include cases such as:

- `NoInitialSweep`
- `NoExpansionFvg`
- `FvgFilledBeforeSetupAcceptance`
- `NoPostObLiquidity`
- `MitigatedBeforeSetupReady`
- `InvalidRiskDistance`
- `ExpiredBeforeMitigation`
- `OpenAtEnd`

## Analysis

- JSONL journals are the source of truth.
- `DeterministicAnalyzer` loads journals into DuckDB for joins and reports.
- DuckDB is analyzer-only. Core engines do not depend on DuckDB.
- Reports include:
  - accepted setups;
  - terminal rejects by reason;
  - trades and outcomes;
  - ambiguous skip rate;
  - long vs short performance;
  - UTC hour buckets;
  - setup feature comparisons between winners and losers.
- Named trading sessions are future work.
- The first AI layer is deterministic post-run analysis over structured journals. It compares winner/loser traits and does not gate trades in the MVP.

## Public Interfaces

- `CandleEvent`: OHLCV, UTC open time, canonical market ID, exchange ID, timeframe, source, run ID.
- `StructureEvent`: B output for swings, liquidity, sweeps, OBs, expansions, FVGs, fill updates, and lifecycle transitions.
- `StructureSnapshot`: B internal/current-state output consumed by C.
- `SetupEvaluation`: C output for accepted or terminally rejected setups, with rule results, `initialSweepSource`, `displacementConfirmationMode`, and feature snapshot.
- `TradeIntent`: D output with status, direction, entry, SL, TP, risk, position size, source setup ID, and rejection reason when applicable.
- `BacktestOutcome`: E1 output with TP, SL, no-fill, open-at-end, expired, or ambiguous-skip status and ML label eligibility.
- `AnalysisReport`: deterministic post-run report over setup/trade/outcome journals.

## Verification Plan

- Unit-test Phase 0 project wiring and CLI parsing.
- Unit-test `markets.json` validation.
- Unit-test A1 candle normalization, ordering, deduplication, and gap journaling.
- Unit-test B structure detection:
  - swings;
  - EQH/EQL tolerance;
  - direction-specific liquidity sweeps;
  - OB detection;
  - expansion/FVG detection;
  - FVG fill updates;
  - OB lifecycle transitions.
- Unit-test C setup rules and terminal rejects.
- Unit-test D trade decisions, risk distance validation, position sizing, and pending-order creation.
- Unit-test E1 TP, SL, no-fill, open-at-end, expiry, and `ambiguous_skip` outcomes.
- Unit-test DuckDB analyzer queries over journaled setup/trade/outcome records.

## Golden Samples

Golden samples are stored as one folder per sample:

```text
WickdBot.Tests/Fixtures/Golden/001-btcusdt-5m-bullish-ob/
  candles.jsonl
  expected.json
  screenshot.png
  notes.md
```

Screenshots are human references only. Tests assert candle-derived behavior.

Expected assertions should be broad behavioral checks first, not brittle full-output JSONL snapshots. Use exact candle timestamps from fixtures where possible and price tolerances where chart annotations may differ.

### Golden Sample 001

Metadata:

- Market: `BTC_USDT_PERP`
- Exchange: Binance
- Timeframe: `5m`
- Window: May 6-7, 2026 UTC
- Direction: bullish

Expected behavior:

- Detect bullish OB using full high/low zone.
- Detect initial sell-side sweep tied to OB or expansion.
- Detect expansion candle and directly tied classic FVG.
- Detect multiple post-OB sell-side liquidity levels above the bullish OB zone.
- Accept actionable setup before OB mitigation once required liquidity exists between price and OB.
- Create pending long order at OB near edge.
- Fill when price wick-touches the OB zone.
- Consume the OB on first mitigation.
- Produce successful TP outcome for the sample.

### Golden Sample 002

Status: planned extension / not MVP-blocking.

This sample represents the same bullish OB strategy family, but the displacement confirmation does not happen through one single expansion candle tied to a classic FVG. Instead, displacement unfolds as a multi-candle impulse away from the OB.

Expected future behavior:

- Detect bullish OB with `initialSweepSource = ObCandleSweep`.
- Support `displacementConfirmationMode = MultiCandleImpulse` as an alternate displacement confirmation mode.
- Detect post-OB sell-side liquidity forming above the bullish OB zone.
- Allow price to take post-OB sell-side liquidity while front-running the OB near-edge entry.
- Keep the pending order alive after the front-run liquidity grab if the OB has not been mitigated.
- Fill only on later wick-touch mitigation of the OB zone, subject to expiry/end-of-run rules.

More golden samples should be added as strategy validation grows.

## Explicit MVP Exclusions

- No multi-market run.
- No multi-timeframe run.
- No higher-timeframe/trend context filter.
- No live executable path.
- No synthetic backtest fees.
- No synthetic backtest slippage.
- No leverage or margin modeling.
- No CSV import/export.
- No Parquet in MVP.
- No adaptive swing model.
- No liquidity-target TP.
- No setup invalidation when target liquidity is taken before entry.

## Future Work

- `AsyncEventBus` using channels for live/evented runtime.
- A2 live data source with allowlisted live markets.
- E2 live execution.
- Hyperliquid BTC/ETH live allowlist.
- Parquet export/cache optimization.
- Adaptive/volatility-based swing detection.
- Higher-timeframe context.
- Liquidity-target exits.
- Stale setup invalidation when target liquidity is already taken.
- Optional FVG max-fill filters based on analysis.
- Named trading sessions.
- Recording real live execution fees and slippage.
