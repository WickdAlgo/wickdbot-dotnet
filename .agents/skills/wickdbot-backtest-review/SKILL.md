---
name: wickdbot-backtest-review
description: Review, analyze, and brainstorm feedback on WickdBot backtest results and journals. Use when the user asks questions about a WickdBot backtest run, structures.jsonl, setup/trade/outcome journals, liquidity sweep and breakout labels, label meanings, event counts, transition rates, failure reasons, or qualitative strategy observations from backtest output.
---

# WickdBot Backtest Review

## Purpose

Help the user ask precise questions about WickdBot backtest output while preserving WickdBot's label semantics. Prefer deterministic counts from JSONL journals, then add clearly marked interpretation or brainstorming.

## Core Workflow

1. Identify the run or artifact: a `runs/{runId}/` directory, a `structures.jsonl` file, or future `setups.jsonl`, `trades.jsonl`, `outcomes.jsonl`, `run.json`, and `analysis.json` files.
2. Read `references/backtest-labels.md` when the question mentions labels, sweep candidates, breakouts, setup states, trade outcomes, or label meaning.
3. Use `scripts/summarize_backtest.py` for count and transition questions over `structures.jsonl`.
4. Distinguish event rows from unique lifecycle occurrences. For staged liquidity questions, count unique `entityId` values such as `breach-000001`, not every row.
5. Answer with the factual result first. Put creative comments, hypotheses, or experiment ideas under a separate heading such as `Interpretation` or `Ideas`.
6. If a requested journal is missing, say it is missing or not implemented in the current run. Do not treat a missing file as zero outcomes.

## Counting Questions

For questions like "how many times did a sweep candidate confirm and how many became an actual breakout?", run:

```bash
python .agents/skills/wickdbot-backtest-review/scripts/summarize_backtest.py runs/<runId>
```

Use the output as follows:

- `sweep candidates formed` means `BuySideSweepCandidate` or `SellSideSweepCandidate` occurred.
- `candidate -> sweepConfirmed` means a formed candidate later reached `BuySideSweepConfirmed` or `SellSideSweepConfirmed`.
- `candidate -> breakoutConfirmed` means a formed candidate later reached `BuySideBreakoutConfirmed` or `SellSideBreakoutConfirmed`.
- `breakout without prior candidate` means the breach accepted beyond the level before a sweep candidate formed.

When the user's wording is ambiguous, answer with the exact label names so the distinction is visible.

## Quant Setup Strategy Evaluation

When evaluating WickdBot setup candidates for the user's current quant-agent strategy, do not force a setup list. If the dataset does not contain a valid setup under the requested rules, say `Pass` or `No valid setup` and explain the missing prerequisite.

Use this compressed strategy definition:

Scan for reversal setups where the local setup area has no opposite-side liquidity left open after the order block and displacement sequence. This is an area-clearance rule, not a requirement that a specific sweep label must appear first. For shorts, no local buy-side liquidity should remain open after the bearish order block and bearish displacement; the terminal swing high in the setup area must be made by the order-block candle or by the bearish displacement/expansion candle. For longs, no local sell-side liquidity should remain open after the bullish order block and bullish displacement; the terminal swing low in the setup area must be made by the order-block candle or by the bullish displacement/expansion candle. Displacement can be a single breakout/breakdown candle with an FVG or a same-direction multi-candle impulse. In a multi-candle impulse, all displacement candles should maintain the same directional momentum: bearish setups require bearish displacement candles, and bullish setups require bullish displacement candles. The order block is higher quality when its candle later becomes the protected finalized swing high/low, price creates new finalized swings or liquidity away from the zone before returning, and the move away reaches meaningful excursion before first mitigation. If the area-clearance and OB-to-displacement relationship are not present, pass instead of stretching weaker OBs into setups.

For this strategy, interpret opposite-side liquidity clearance as:

- Long setup: inspect local swing lows and sell-side liquidity in the setup area. The terminal swing low must be the bullish order-block candle or bullish displacement/expansion candle, and no earlier local sell-side liquidity may remain open after the displacement sequence.
- Short setup: inspect local swing highs and buy-side liquidity in the setup area. The terminal swing high must be the bearish order-block candle or bearish displacement/expansion candle, and no earlier local buy-side liquidity may remain open after the displacement sequence.
- `sellSideSweepCandidate`, `sellSideRejectionConfirmed`, `sellSideSweepConfirmed`, `buySideSweepCandidate`, `buySideRejectionConfirmed`, and `buySideSweepConfirmed` are useful evidence, but they are not required by themselves.
- `sellSideBreakoutConfirmed` and `buySideBreakoutConfirmed` are not automatic rejection reasons. They can be valid when the breakout is the mechanism that clears local opposite-side liquidity and the resulting terminal extreme belongs to the order-block/displacement sequence.
- Reject the setup when older local opposite-side swing liquidity remains uncleared between the setup-area start and displacement completion, or when the terminal extreme belongs to an unrelated prior swing rather than the order-block/displacement sequence.

When scanning active/actionable setups:

- Only list unmitigated order blocks that still have a valid limit-order side relative to the latest available candle or explicitly checked live price.
- Do not reject an unmitigated parent order block only because a fresher nested order block formed below or above it later. Parent and nested order blocks can both be valid actionable setups when each has clean local opposite-side liquidity clearance, a clean OB-to-displacement relationship, and a valid limit-order side.
- Separate hierarchy from validity: label older unmitigated zones as parent/context setups when useful, but keep them in the actionable list if they still satisfy the strategy. Rank fresher nested setups separately instead of using them to invalidate the parent.
- Do not list stale unmitigated zones when local opposite-side liquidity clearance or the OB-to-displacement relationship has become unclear.
- Separate strict `SingleExpansionFvg` candidates from broader `MultiCandleImpulse` candidates.
- Treat OHLC same-candle target/stop outcomes as ambiguous unless the candle path is known.

## Quick Structure Filters

For quick PowerShell inspection of a single structure event type, use `Get-Content` with JSON conversion. Replace `<runId>` and `<eventType>` with the target run and structure label, for example `phase-3-refined-smoke` and `bullishExpansion`.

```powershell
Get-Content .\runs\<runId>\structures.jsonl |
  ForEach-Object { $_ | ConvertFrom-Json } |
  Where-Object { $_.eventType -eq "<eventType>" }
```

To count only that structure type:

```powershell
Get-Content .\runs\<runId>\structures.jsonl |
  ForEach-Object { $_ | ConvertFrom-Json } |
  Where-Object { $_.eventType -eq "<eventType>" } |
  Measure-Object
```

## Review Style

- Start with run context: file path, run ID if available, market, timeframe, record count, and any schema warnings.
- Report counts and percentages together when useful.
- Compare buy-side and sell-side separately when the result could hide directional asymmetry.
- Call out whether the analysis is structure-only. Phase 3 currently writes `structures.jsonl` and stops before setup, trade, settlement, and deterministic analysis journals.
- Treat old or hand-split journal files as secondary evidence. If labels do not match the current `StructureEventType` enum, flag them as legacy/mismatched before drawing conclusions.
- For brainstorming, suggest testable follow-ups: side splits, hour/session buckets, candidate-to-breakout rates, rejection-to-sweep delays, protected-swing distance, OB/FVG proximity, or parameter sensitivity.

## Resources

- `references/backtest-labels.md`: WickdBot label vocabulary and current/planned journal semantics.
- `scripts/summarize_backtest.py`: deterministic JSONL summary for event counts and staged liquidity transitions.
