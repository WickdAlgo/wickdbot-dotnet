# Phase 2: Historical Candle Fetching, Cache, and Replay

Date: 2026-05-12

## Goal

Turn validated Phase 1 run requests into a real deterministic candle stream. Implement A1 historical data handling so Wickd can fetch configured market candles, cache normalized JSONL, and replay cached candles for later backtest phases.

## Planned Scope

- Add A1 data-source contracts and results:
  - `IMarketDataClient`
  - a raw fetched candle DTO, if useful
  - `HistoricalDataSource`
  - a fetch/load result summary that includes candle count, cache path, and detected gaps.
- Add the first production market data client for the default Binance market path, hidden behind `IMarketDataClient`.
- Return clear unsupported-exchange errors for configured markets whose exchange IDs do not yet have a Phase 2 client.
- Fetch only completed historical candles for one market and one timeframe per request.
- Convert fetched exchange candles into `CandleEvent` records with UTC open time, decimal OHLCV values, configured market identity, timeframe, and `source=Historical`.
- Reuse Phase 1 normalization and JSONL I/O before accepting or caching candles.
- Implement deterministic cache behavior:
  - use `RunRequest.CandleCachePath`;
  - read existing cache when present;
  - fetch and write the cache on miss;
  - write only normalized candles;
  - keep gap metadata available to callers.
- Add a replay path that reads cached historical candles and emits backtest-source candles with a supplied run ID.
- Extend `fetch` from placeholder behavior to functional cache creation with concise command output.
- Extend `backtest` only enough to load and replay candle input before returning placeholder behavior.
- Add a small local dataset alias layer so fetched ranges can be named and later replayed by alias.
- Add focused tests for market data client mapping, cache hit/miss behavior, normalization before caching, unsupported exchanges, replay source/run ID conversion, and CLI fetch/backtest data validation.
- Keep these out of Phase 2:
  - strategy structure detection;
  - setup evaluation;
  - trade decisions;
  - settlement;
  - DuckDB analysis;
  - live data;
  - multi-market or multi-timeframe runs.

## Completed Work

- Added Phase 2 historical data contracts and results for market data clients, raw exchange candles, fetch/cache summaries, replay summaries, and expected data failures.
- Added the `ccxt` NuGet dependency and a Binance CCXT client that preserves Wickd's configured `binance` exchange identity while using CCXT's Binance USD-M adapter internally.
- Implemented deterministic historical cache behavior: cache hit reads existing JSONL, cache miss fetches public OHLCV, all accepted candles are normalized before returning or writing, and gap metadata remains available.
- Added completed-candle validation so requests whose exclusive `to` includes incomplete candles fail before fetching.
- Added replay behavior that requires an existing cache, validates cached historical candle identity, and emits backtest-source candles with the supplied run ID.
- Extended `fetch` to create/reuse candle caches and `backtest` to replay cached input before later strategy phases exist.
- Added a generated `data/datasets.json` alias catalog, `fetch --alias`, `fetch --force`, and `backtest --dataset` so repeated backtests can reuse a named cached range without retyping market, timeframe, and UTC range arguments.
- Added focused xUnit coverage for cache hit/miss behavior, unsupported exchanges, completed-candle validation, normalization before caching, gap reporting, replay conversion, CLI behavior, and CCXT Binance mapping/conversion.
- Added `.gitignore` entries for generated Wickd runtime outputs under `data/datasets.json`, `data/cache/`, and `runs/`.

## Validation

Phase 2 is complete when these commands pass:

```text
dotnet restore Wickd.slnx
dotnet test Wickd.slnx
dotnet run --project src/Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z
dotnet run --project src/Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z
dotnet run --project src/Wickd.Cli -- backtest --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --run-id phase-2-smoke
```

Current validation on 2026-05-12:

```text
dotnet restore .\Wickd.slnx
dotnet test .\Wickd.slnx --no-restore
dotnet run --no-build --project .\src\Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z
dotnet run --no-build --project .\src\Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z
dotnet run --no-build --project .\src\Wickd.Cli -- backtest --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --run-id phase-2-smoke
```

Results:

- Restore passed.
- Test suite passed with 48 tests.
- First `fetch` created 372 cached candles with 0 gaps.
- Repeated `fetch` loaded the same 372 candles from cache with 0 gaps.
- `backtest` replayed 372 cached candles for `phase-2-smoke` with 0 gaps, then stopped before strategy execution.

The code should reliably prove:

- `fetch` can create the deterministic candle cache for the requested market, timeframe, and UTC range;
- a repeated `fetch` can reuse the existing cache without refetching;
- cached candles are normalized before they are accepted;
- `backtest` can load and replay cached candles before later strategy phases exist;
- unsupported exchanges and invalid candle data fail with clear messages.

Record the result when validation has been run.

Dataset alias update on 2026-05-13:

```text
dotnet build .\Wickd.slnx --no-restore
dotnet test .\Wickd.slnx --no-restore
dotnet run --no-build --project .\src\Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --alias may6-session --force
```

Results:

- Build passed with 0 warnings and 0 errors.
- Test execution and CLI smoke execution were blocked by Windows Application Control when loading the freshly built `Wickd.dll` with `0x800711C7`.
- Alias-specific test coverage has been added, but it still needs to be executed in an environment that allows the built assembly to load.

## Notes

Phase 2 is still data plumbing, not trading logic. It should establish trustworthy candle input for the future `StructureEngine`, `SetupEngine`, `TradeEngine`, and `BacktestSettlementEngine`.

Use public historical market data only. Do not introduce API keys, account access, order execution, or live subscriptions in this phase.
