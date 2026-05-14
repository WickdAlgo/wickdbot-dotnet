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
