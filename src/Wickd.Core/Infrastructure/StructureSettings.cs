#nullable enable

namespace Wickd.Infrastructure;

/// <summary>
/// Represents validated market-structure detection parameters.
/// </summary>
/// <param name="MinimumSwingSeparationCandles">Minimum candle distance required between same-side anchors and the intervening opposite swing.</param>
/// <param name="EqualLevelToleranceBasisPoints">Maximum distance for equal high/low liquidity, in basis points.</param>
/// <param name="OrderBlockSearchBackCandles">Maximum candles to search backward for an order-block candle.</param>
/// <param name="ExpansionLookbackCandles">Prior candles used to compute average range for expansion detection.</param>
/// <param name="ExpansionBodyToAverageRange">Minimum body-to-average-range ratio for expansion detection.</param>
/// <param name="ExpansionFvgWindowCandles">Maximum candles between an order-block candle and the expansion/FVG middle candle.</param>
public sealed record StructureSettings(
    int MinimumSwingSeparationCandles,
    decimal EqualLevelToleranceBasisPoints,
    int OrderBlockSearchBackCandles,
    int ExpansionLookbackCandles,
    decimal ExpansionBodyToAverageRange,
    int ExpansionFvgWindowCandles);
