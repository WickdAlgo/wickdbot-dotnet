# Phase 3: Structure Engine and Structure Journaling

Date: 2026-05-14

## Goal

Turn replayed historical candles into deterministic, market-agnostic structure events. Implement the B layer so WickdBot can detect swings, liquidity, sweeps, order blocks, expansion/FVG events, and OB/FVG lifecycle updates through configurable parameters, without making setup or trade decisions.

## Planned Scope

- Add structure contracts and shared types:
  - `StructureEngine`
  - `StructureEvent`
  - `StructureEventType`
  - `StructureSnapshot`, if useful for later setup evaluation
  - structure-specific IDs such as `swing-000001`, `liq-000001`, `ob-000001`, and `fvg-000001`.
- Bind and validate structure settings from `appsettings.json`:
  - `MinimumSwingSeparationCandles`
  - `EqualLevelToleranceBasisPoints`
  - `OrderBlockSearchBackCandles`
  - `ExpansionLookbackCandles`
  - `ExpansionBodyToAverageRange`
  - `ExpansionFvgWindowCandles`.
- Keep all Phase 3 detection thresholds, windows, tolerances, and lifecycle limits parameterized through configuration instead of hard-coded strategy constants.
- Treat committed `appsettings.json` values as generic reference defaults, not optimized trading parameters.
- Add support for a non-committed local appsettings override file for private parameter sets, without using `.env` or storing secrets.
- Validate candle stream assumptions before structure processing:
  - one market and one timeframe per run;
  - UTC open times;
  - strictly increasing candle order after Phase 2 replay;
  - no duplicate candle open times.
- Implement alternating swing sequencing:
  - track candidate swing highs/lows;
  - update candidates when price extends farther;
  - finalize the intervening opposite swing when a prior same-side swing is broken;
  - leave the far-right unresolved swing as a candidate.
- Implement equal high and equal low liquidity from finalized swings with configured basis-point tolerance and journal the actual level distance.
- Implement staged liquidity-taking classification:
  - Stage 1 breach when price trades through finalized buy-side or sell-side liquidity;
  - Stage 2 sweep candidate when price closes back inside the swept level/range;
  - Stage 3 same-timeframe rejection confirmation through opposite displacement or opposite FVG;
  - Stage 4 structural sweep confirmation when the protected/intervening opposite swing breaks;
  - breakout confirmation when price accepts beyond the breached level before protected structure breaks.
- Implement order block discovery:
  - bullish OB uses the last qualifying bearish candle;
  - bearish OB uses the last qualifying bullish candle;
  - doji/neutral candles are not OBs;
  - zones use the full candle high/low;
  - search backward only within the configured window.
- Implement single-expansion/FVG structure detection:
  - expansion body must meet the configured recent-average-range threshold;
  - recent average range uses only prior candles;
  - expansion candle must close beyond the OB zone in the displacement direction;
  - required FVG uses the classic three-candle definition;
  - expansion candle is the middle candle of the required FVG.
- Track FVG fill percentage updates using wick penetration.
- Track OB lifecycle transitions for active, mitigated, consumed, and invalidated or rejected states when Phase 3 rules can prove them.
- Add `structures.jsonl` writing for backtest runs.
- Extend `backtest` so it can run `A1 -> B`, write structure events, and then stop before setup evaluation.
- Add focused tests for swing detection, EQH/EQL tolerance, liquidity sweeps, OB detection, expansion/FVG detection, FVG fill updates, OB lifecycle transitions, event ordering, deterministic IDs, and `structures.jsonl` round-tripping.
- Keep these out of Phase 3:
  - actionable setup acceptance;
  - terminal setup rejects;
  - trade intent creation;
  - settlement outcomes;
  - DuckDB analysis;
  - live data;
  - multi-market or multi-timeframe runs.

## Completed Work

- Added validated `StructureSettings` binding from `WickdBot:Structure` in `appsettings.json`.
- Added optional `appsettings.Local.json` support next to the committed settings file so private local structure parameters can override defaults without `.env` or source-control changes.
- Added the B-layer structure contracts and engine for deterministic structure processing:
  - structure events and event types;
  - bullish/bearish direction metadata;
  - OB and FVG lifecycle states;
  - final structure snapshots and processing summaries.
- Added deterministic ID assignment for structure events, swing candidates/finals, liquidity, breaches, order blocks, and FVGs.
- Added replayed candle-stream validation before structure processing:
  - backtest-source candles only;
  - one run ID, market, exchange, and timeframe;
  - UTC open times;
  - strictly increasing open times.
- Implemented configured detection for:
  - alternating swing candidates, candidate updates, and finalized swings;
  - EQH/EQL liquidity from finalized swings;
  - staged buy-side and sell-side liquidity breaches, sweep candidates, rejection confirmations, sweep confirmations, and breakout confirmations;
  - bullish and bearish order block discovery;
  - single-expansion/FVG confirmation;
  - FVG fill percentage updates;
  - OB mitigation and consumption.
- Added `StructureJsonLines` for deterministic `structures.jsonl` write/read behavior.
- Added a Phase 3 `BacktestPipeline` that runs `A1 -> B`, writes `runs/{runId}/structures.jsonl`, and stops before setup/trade execution.
- Extended `backtest` CLI output to report replay count, structure event count, and structure journal path.
- Added focused xUnit coverage for configuration binding/overrides, alternating swing sequencing, staged liquidity classification, structure validation and detection, structure JSONL round-tripping, and CLI backtest journaling.

## Validation

Phase 3 is complete when these commands pass:

```text
dotnet test WickdBot.slnx
dotnet run --project src/WickdBot -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --alias may6-session
dotnet run --project src/WickdBot -- backtest --dataset may6-session --run-id phase-3-smoke
```

The code should reliably prove:

- replayed candles can produce deterministic structure events;
- `runs/phase-3-smoke/structures.jsonl` is written and can be read back;
- structure event IDs are stable for the same candle input;
- configured structure settings affect detection in predictable ways;
- local appsettings overrides can change detection parameters without editing committed source files;
- invalid candle streams fail before structure processing;
- `backtest` still does not create setups, trades, outcomes, or analysis output in this phase.

Record the result when validation has been run.

Current validation on 2026-05-14:

```text
dotnet test .\WickdBot.slnx
dotnet run --project .\src\WickdBot -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --alias may6-session --force
dotnet run --project .\src\WickdBot -- backtest --dataset may6-session --run-id phase-3-smoke
dotnet run --no-restore --project .\src\WickdBot -- backtest --dataset may6-session --run-id phase-3-refined-smoke
dotnet run --no-restore --project .\src\WickdBot -- backtest --dataset may6-session --run-id phase-3-swing-settings-smoke
```

Results:

- Test suite passed with 94 tests after the run ID and swing-setting review cleanup.
- `fetch` saved dataset alias `may6-session` and wrote 372 cached candles with 0 gaps.
- Initial `backtest` replayed 372 cached candles and wrote 518 structure events to `runs/phase-3-smoke/structures.jsonl`.
- Refined swing/liquidity `backtest` replayed 372 cached candles and wrote 726 structure events to `runs/phase-3-refined-smoke/structures.jsonl`.
- Swing-separation `backtest` replayed 372 cached candles and wrote 504 structure events to `runs/phase-3-swing-settings-smoke/structures.jsonl`.
- Backtest still stops before setup evaluation, trade creation, settlement outcomes, and analysis output.

## Notes

Phase 3 is still neutral market-structure work. It should describe what happened in the candle stream, not whether WickdBot should trade it.

Open-source boundary: the repository can expose the infrastructure, event model, and generic detection rules, but it should not encode private tuned parameter sets or future proprietary setup acceptance filters. Public defaults should be useful for development and examples, not presented as the "best" SMC settings.

The next likely phase is setup evaluation: consuming structure events and snapshots to decide whether an OB/FVG sequence becomes an actionable setup or a terminal reject.
