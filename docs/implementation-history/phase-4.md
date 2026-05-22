# Phase 4: Setup Engine and Setup Journaling

Date: 2026-05-18

## Goal

Turn Phase 3 structure output into deterministic setup evaluations. Implement the C layer so Wickd can accept generic OB/FVG setups or emit terminal setup rejects, while still stopping before trade creation.

## Planned Scope

- Add setup contracts and shared types:
  - `SetupEngine`
  - `SetupEvaluation`
  - `SetupEvaluationStatus`
  - `SetupRejectReason`
  - `InitialSweepSource`
  - `DisplacementConfirmationMode`
  - setup-specific IDs such as `setup-000001`.
- Bind and validate setup settings from `appsettings.json`:
  - `EnableFvgMaxFillBeforeEntryFilter`
  - `FvgMaxFillBeforeEntryPercent`.
- Keep Phase 4 setup thresholds and optional filters parameterized through configuration instead of hard-coded tuned strategy constants.
- Treat committed `appsettings.json` setup values as generic reference defaults, not optimized trading parameters.
- Preserve support for non-committed local appsettings overrides so private setup parameters can stay outside source control.
- Consume Phase 3 structure output in deterministic order:
  - discovered order blocks;
  - expansion and FVG confirmations;
  - swing and equal-level liquidity;
  - liquidity sweep classifications;
  - FVG fill updates;
  - OB mitigation and consumption events.
- Implement candidate setup tracking from discovered bullish and bearish order blocks.
- Implement generic setup acceptance rules:
  - a candidate must have a direction-specific initial liquidity sweep by the selected OB candle or the expansion candle;
  - a candidate must have a qualifying single-expansion/FVG confirmation tied to the OB;
  - a candidate must have post-OB direction-appropriate swing or equal-level liquidity between current price and the OB mitigation zone;
  - a candidate can become actionable before OB mitigation once all required conditions are true.
- Implement terminal setup rejects:
  - reject when the OB is mitigated before the setup is ready;
  - reject when the required FVG is fully filled before setup acceptance;
  - reject when the optional configured FVG max-fill filter is enabled and exceeded before setup acceptance.
- Add `setups.jsonl` writing and reading for backtest runs.
- Extend `backtest` so it can run `A1 -> B -> C`, write setup evaluations, and then stop before trade intent creation.
- Update CLI backtest output to report replay count, structure event count, setup evaluation count, and journal paths.
- Add focused tests for setup settings, accepted bullish and bearish setups, terminal rejects, optional FVG max-fill behavior, post-OB liquidity qualification, deterministic setup IDs, event ordering, and `setups.jsonl` round-tripping.
- Keep these out of Phase 4:
  - private or optimized setup acceptance filters;
  - setup scoring or ranking;
  - stale setup expiry;
  - trade intent creation;
  - settlement outcomes;
  - DuckDB analysis;
  - live data;
  - multi-market or multi-timeframe runs.

## Completed Work

Not started yet.

## Validation

Phase 4 is complete when these commands pass:

```text
dotnet test Wickd.slnx
dotnet run --project src/Wickd.Cli -- fetch --market BTC_USDT_PERP --timeframe 5m --from 2026-05-06T00:00:00Z --to 2026-05-07T07:00:00Z --alias may6-session
dotnet run --project src/Wickd.Cli -- backtest --dataset may6-session --run-id phase-4-smoke
```

The code should reliably prove:

- replayed candles can produce deterministic structure events and setup evaluations;
- `runs/phase-4-smoke/structures.jsonl` is written and can be read back;
- `runs/phase-4-smoke/setups.jsonl` is written and can be read back;
- setup evaluation IDs are stable for the same structure input;
- configured setup settings affect setup rejection in predictable ways;
- local appsettings overrides can change setup parameters without editing committed source files;
- accepted setups and terminal rejects are journaled without noisy "not ready" records;
- `backtest` still does not create trades, outcomes, or analysis output in this phase.

Record the result when validation has been run.

## Notes

Pre-Phase 4 architecture migration completed on 2026-05-22. See `docs/implementation-history/2026-05-22-wickd-technical-rename.md` for the detailed rename and package-split record.

Phase 4 is the first decision-making layer, but it should remain a generic open-source setup evaluator rather than a tuned private strategy. The repository can expose the contracts, journal format, pipeline wiring, and reference OB/FVG rules; private edge should stay in local configuration or future private filters outside the committed codebase.

The next likely phase is trade intent creation: converting accepted setup evaluations into deterministic pending trade decisions with entry, stop loss, take profit, and risk metadata.
