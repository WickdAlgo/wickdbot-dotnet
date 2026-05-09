using WickdBot.Models;

namespace WickdBot.Data;

/// <summary>
/// Produces deterministic candle order and reports non-fatal timeframe gaps.
/// </summary>
internal static class CandleNormalizer
{
    /// <summary>
    /// Sorts candles by open time, collapses exact duplicates, rejects conflicting duplicates, and detects gaps.
    /// </summary>
    /// <param name="candles">Candles to normalize.</param>
    /// <param name="timeframe">Expected candle timeframe.</param>
    /// <returns>Normalized candles with detected gap metadata.</returns>
    /// <exception cref="ArgumentException">Thrown when candles contain non-UTC timestamps or conflicting duplicates.</exception>
    internal static CandleNormalizationResult Normalize(IEnumerable<CandleEvent> candles, Timeframe timeframe)
    {
        var sortedCandles = candles
            .OrderBy(candle => candle.OpenTimeUtc)
            .ToArray();

        var normalizedCandles = new List<CandleEvent>(sortedCandles.Length);
        var gaps = new List<CandleGap>();

        foreach (var candle in sortedCandles)
        {
            if (candle.OpenTimeUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException("Candle open time must be expressed in UTC.", nameof(candles));
            }

            if (normalizedCandles.Count > 0)
            {
                var previous = normalizedCandles[^1];

                if (candle.OpenTimeUtc == previous.OpenTimeUtc)
                {
                    if (!AreEquivalent(candle, previous))
                    {
                        throw new ArgumentException(
                            $"Conflicting candles share open time {candle.OpenTimeUtc:O}.",
                            nameof(candles));
                    }

                    continue;
                }

                var expectedOpenTimeUtc = previous.OpenTimeUtc + timeframe.Duration;
                if (candle.OpenTimeUtc < expectedOpenTimeUtc)
                {
                    throw new ArgumentException(
                        $"Non-increasing candle timestamp {candle.OpenTimeUtc:O} after {previous.OpenTimeUtc:O}.",
                        nameof(candles));
                }

                if (candle.OpenTimeUtc > expectedOpenTimeUtc)
                {
                    gaps.Add(new CandleGap(
                        expectedOpenTimeUtc,
                        candle.OpenTimeUtc,
                        CountMissingCandles(expectedOpenTimeUtc, candle.OpenTimeUtc, timeframe.Duration)));
                }
            }

            normalizedCandles.Add(candle);
        }

        return new CandleNormalizationResult(normalizedCandles, gaps);
    }

    private static int CountMissingCandles(
        DateTimeOffset expectedOpenTimeUtc,
        DateTimeOffset actualOpenTimeUtc,
        TimeSpan timeframeDuration)
    {
        var missingInterval = actualOpenTimeUtc - expectedOpenTimeUtc;
        if (missingInterval <= TimeSpan.Zero)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(missingInterval.Ticks / (double)timeframeDuration.Ticks));
    }

    private static bool AreEquivalent(CandleEvent left, CandleEvent right)
    {
        return left.OpenTimeUtc == right.OpenTimeUtc
            && left.MarketId == right.MarketId
            && left.ExchangeId == right.ExchangeId
            && left.Timeframe == right.Timeframe
            && left.Open == right.Open
            && left.High == right.High
            && left.Low == right.Low
            && left.Close == right.Close
            && left.Volume == right.Volume
            && left.Source == right.Source
            && left.RunId == right.RunId;
    }
}
