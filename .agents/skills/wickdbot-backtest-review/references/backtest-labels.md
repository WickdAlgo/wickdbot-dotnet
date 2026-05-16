# WickdBot Backtest Labels

Use this reference when answering questions about WickdBot backtest journals. Prefer the current C# contracts in `src/WickdBot/Engines/` if they differ from this file.

## Current Implemented Journal

Phase 3 currently writes:

- `runs/{runId}/structures.jsonl`

Planned but not implemented in Phase 3:

- `setups.jsonl`
- `trades.jsonl`
- `outcomes.jsonl`
- `run.json`
- `analysis.json`

Do not report missing planned files as zero results. Say they are missing or not implemented for that run.

## Structure Event Schema

Current `structures.jsonl` rows use camelCase JSON from `StructureEvent`:

- Identity: `runId`, `sequence`, `eventId`, `eventType`
- Time: `observedOpenTimeUtc`, `subjectOpenTimeUtc`, `sourceOpenTimesUtc`
- Market: `marketId`, `exchangeId`, `timeframe`
- Entity links: `entityId`, `relatedEntityIds`, `breachedLiquidityId`, `originalBreachEventId`, `protectedSwingId`
- Values: `direction`, `price`, `zoneLow`, `zoneHigh`, `fillPercent`, `distanceBasisPoints`
- State labels: `orderBlockState`, `fvgState`, `classificationStage`

For staged liquidity, group by `entityId` (`breach-*`). `originalBreachEventId` links later lifecycle rows to the first breach event. `breachedLiquidityId` points to the swept liquidity, usually a finalized swing ID (`swing-*`) or equal-liquidity ID (`liq-*`).

## Current StructureEventType Values

Swing labels:

- `swingHighCandidate`, `swingLowCandidate`: a candidate swing was created.
- `swingHighCandidateUpdated`, `swingLowCandidateUpdated`: the candidate moved to a more extreme price.
- `swingHighFinalized`, `swingLowFinalized`: a swing became structurally finalized and can become liquidity.

Liquidity labels:

- `equalHighLiquidity`: finalized swing highs are close enough to form buy-side liquidity.
- `equalLowLiquidity`: finalized swing lows are close enough to form sell-side liquidity.
- `buySideLiquidityBreached`: price traded through buy-side liquidity above highs or equal highs.
- `sellSideLiquidityBreached`: price traded through sell-side liquidity below lows or equal lows.

Staged liquidity classification:

- `buySideSweepCandidate`, `sellSideSweepCandidate`: after a breach, price closed back inside the swept level or range. This is a candidate, not a final sweep.
- `buySideRejectionConfirmed`, `sellSideRejectionConfirmed`: same-timeframe rejection was confirmed after a sweep candidate by opposite displacement or opposite FVG. This is intermediate and can still become `sweepConfirmed`; after this state the current engine does not allow breakout confirmation.
- `buySideSweepConfirmed`, `sellSideSweepConfirmed`: the protected/intervening opposite swing broke after a candidate or rejection confirmation. This is the structural sweep terminal state.
- `buySideBreakoutConfirmed`, `sellSideBreakoutConfirmed`: price accepted beyond the breached liquidity level before protected structure broke. This is the breakout terminal state. It can happen with no prior sweep candidate, or after a candidate but before rejection/sweep confirmation.

Order block and FVG labels:

- `bullishOrderBlockDiscovered`, `bearishOrderBlockDiscovered`: a qualifying opposite-color candle was selected as an OB.
- `bullishExpansion`, `bearishExpansion`: an expansion candle was confirmed with the required FVG.
- `bullishFvgDiscovered`, `bearishFvgDiscovered`: a classic three-candle FVG was discovered.
- `fvgFillUpdated`: wick penetration increased the FVG fill percentage.
- `orderBlockMitigated`: a later candle wick-touched an active OB zone.
- `orderBlockConsumed`: the OB was consumed by first mitigation touch.

## ClassificationStage Values

Current common values:

- `candidate`
- `candidateUpdated`
- `finalized`
- `liquidityFinalized`
- `breached`
- `sweepCandidate`
- `rejectionConfirmed`
- `sweepConfirmed`
- `breakoutConfirmed`

For liquidity lifecycle analytics, use only `breached`, `sweepCandidate`, `rejectionConfirmed`, `sweepConfirmed`, and `breakoutConfirmed`.

## Direction Semantics

Buy-side liquidity lives above highs or equal highs. A buy-side sweep candidate/confirmed sweep is bearish in direction because price rejected after taking buy-side stops. A buy-side breakout is bullish because price accepted above buy-side liquidity.

Sell-side liquidity lives below lows or equal lows. A sell-side sweep candidate/confirmed sweep is bullish in direction because price rejected after taking sell-side stops. A sell-side breakout is bearish because price accepted below sell-side liquidity.

## Planned Setup, Trade, and Outcome Labels

From `PLAN.md`, future setup/trade/outcome journals may include:

- Initial sweep source: `ObCandleSweep`, `PostObDisplacementSweep`
- Displacement confirmation mode: `SingleExpansionFvg`, `MultiCandleImpulse`
- Stable reject or terminal categories: `NoInitialSweep`, `NoExpansionFvg`, `FvgFilledBeforeSetupAcceptance`, `NoPostObLiquidity`, `MitigatedBeforeSetupReady`, `InvalidRiskDistance`, `ExpiredBeforeMitigation`, `OpenAtEnd`
- Outcome statuses: TP, SL, no-fill, open-at-end, canceled/expired, ambiguous-skip

Treat these as planned labels unless the corresponding journal files exist in the run.

## Legacy Or Mismatched Artifacts

Some old or manually split artifacts under `runs/` may contain labels such as `swingHigh`, `swingLow`, or `buySideLiquiditySweep`. These do not match the current `StructureEventType` enum. Flag them as legacy/mismatched and avoid mixing them with current staged labels unless the user explicitly asks for a legacy review.
