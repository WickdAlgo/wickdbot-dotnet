#nullable enable

namespace WickdBot.Engines;

/// <summary>
/// Identifies the kind of market-structure event written to structures.jsonl.
/// </summary>
internal enum StructureEventType
{
    /// <summary>
    /// A swing high candidate was created.
    /// </summary>
    SwingHighCandidate,

    /// <summary>
    /// A swing low candidate was created.
    /// </summary>
    SwingLowCandidate,

    /// <summary>
    /// A swing high candidate moved to a higher high.
    /// </summary>
    SwingHighCandidateUpdated,

    /// <summary>
    /// A swing low candidate moved to a lower low.
    /// </summary>
    SwingLowCandidateUpdated,

    /// <summary>
    /// A swing high was structurally finalized.
    /// </summary>
    SwingHighFinalized,

    /// <summary>
    /// A swing low was structurally finalized.
    /// </summary>
    SwingLowFinalized,

    /// <summary>
    /// Equal-high buy-side liquidity confirmed from swing highs.
    /// </summary>
    EqualHighLiquidity,

    /// <summary>
    /// Equal-low sell-side liquidity confirmed from swing lows.
    /// </summary>
    EqualLowLiquidity,

    /// <summary>
    /// Price traded through buy-side liquidity.
    /// </summary>
    BuySideLiquidityBreached,

    /// <summary>
    /// Price traded through sell-side liquidity.
    /// </summary>
    SellSideLiquidityBreached,

    /// <summary>
    /// Price closed back inside after taking buy-side liquidity.
    /// </summary>
    BuySideSweepCandidate,

    /// <summary>
    /// Price closed back inside after taking sell-side liquidity.
    /// </summary>
    SellSideSweepCandidate,

    /// <summary>
    /// Same-timeframe rejection was confirmed after a buy-side sweep candidate.
    /// </summary>
    BuySideRejectionConfirmed,

    /// <summary>
    /// Same-timeframe rejection was confirmed after a sell-side sweep candidate.
    /// </summary>
    SellSideRejectionConfirmed,

    /// <summary>
    /// A buy-side sweep was structurally confirmed.
    /// </summary>
    BuySideSweepConfirmed,

    /// <summary>
    /// A sell-side sweep was structurally confirmed.
    /// </summary>
    SellSideSweepConfirmed,

    /// <summary>
    /// A buy-side liquidity breach was confirmed as a breakout.
    /// </summary>
    BuySideBreakoutConfirmed,

    /// <summary>
    /// A sell-side liquidity breach was confirmed as a breakout.
    /// </summary>
    SellSideBreakoutConfirmed,

    /// <summary>
    /// A bullish order block was discovered.
    /// </summary>
    BullishOrderBlockDiscovered,

    /// <summary>
    /// A bearish order block was discovered.
    /// </summary>
    BearishOrderBlockDiscovered,

    /// <summary>
    /// A bullish expansion candle was confirmed with its required FVG.
    /// </summary>
    BullishExpansion,

    /// <summary>
    /// A bearish expansion candle was confirmed with its required FVG.
    /// </summary>
    BearishExpansion,

    /// <summary>
    /// A bullish fair-value gap was discovered.
    /// </summary>
    BullishFvgDiscovered,

    /// <summary>
    /// A bearish fair-value gap was discovered.
    /// </summary>
    BearishFvgDiscovered,

    /// <summary>
    /// A fair-value gap fill percentage increased.
    /// </summary>
    FvgFillUpdated,

    /// <summary>
    /// An active order block was wick-touched.
    /// </summary>
    OrderBlockMitigated,

    /// <summary>
    /// An order block was consumed by first mitigation.
    /// </summary>
    OrderBlockConsumed
}
