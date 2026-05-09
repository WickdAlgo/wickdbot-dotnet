# Phase 1: Core Contracts, Configuration, and Candle Foundation

Date: 2026-05-09

## Goal

Build the non-strategy foundation for deterministic backtests: validated configuration, canonical market identity, timeframe handling, normalized candle records, JSONL candle I/O, and local candle normalization.

## Planned Scope

- Add core shared models:
  - `CandleEvent`
  - `MarketDefinition`
  - `RunRequest`
  - `Timeframe`
  - `CandleSource`
  - `DateRange`, if useful
- Add a settings loader for `appsettings.json` and `markets.json`.
- Validate duplicate market IDs, missing exchange IDs, missing exchange symbols, unknown markets, invalid timeframes, and invalid date ranges.
- Support MVP timeframe strings:
  - `1m`
  - `5m`
  - `15m`
  - `1h`
  - `4h`
  - `1d`
- Add deterministic JSONL read/write support for candle records.
- Add local candle normalization:
  - sort by UTC open time;
  - deduplicate by open time;
  - reject non-increasing timestamps;
  - detect timeframe gaps;
  - allow gaps by default.
- Extend `fetch` and `backtest` command skeletons so they parse, load configuration, resolve markets, and validate arguments before returning placeholder behavior.
- Add focused tests for configuration validation, timeframe parsing, CLI validation, candle normalization, gap detection, and JSONL round-tripping.

## Completed Work

Not started yet.

## Validation

Phase 1 is complete when:

```text
dotnet test WickdBot.slnx
```

passes and the code can reliably answer:

- what market `BTC_USDT_PERP` resolves to;
- whether `5m` is valid;
- what cache path a requested run would use;
- whether candle records can be normalized into ordered `CandleEvent` objects;
- whether `CandleEvent` JSONL can be written and read back with deterministic backtest values intact.

## Notes

This phase should not implement exchange fetching, strategy detection, trade decisions, settlement, DuckDB analysis, live data, or the async event bus. The safer next phase after this is A1 historical data fetching, caching, and replay, because the rest of the trading pipeline needs realistic normalized candle input.
