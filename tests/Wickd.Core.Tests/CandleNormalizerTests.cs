using Wickd.Data;
using Wickd.Models;

namespace Wickd.Tests;

/// <summary>
/// Verifies deterministic candle normalization behavior.
/// </summary>
public class CandleNormalizerTests
{
    /// <summary>
    /// Confirms candles are sorted by UTC open time.
    /// </summary>
    [Fact]
    public void NormalizeSortsByOpenTime()
    {
        var result = CandleNormalizer.Normalize(
            [
                CreateCandle(new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero)),
                CreateCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ],
            Timeframe.Parse("5m"));

        Assert.Equal(
            [
                new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero)
            ],
            result.Candles.Select(candle => candle.OpenTimeUtc));
    }

    /// <summary>
    /// Confirms exact duplicate candles collapse to one record.
    /// </summary>
    [Fact]
    public void NormalizeCollapsesExactDuplicateCandles()
    {
        var candle = CreateCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero));

        var result = CandleNormalizer.Normalize([candle, candle], Timeframe.Parse("5m"));

        var normalized = Assert.Single(result.Candles);
        Assert.Equal(candle.OpenTimeUtc, normalized.OpenTimeUtc);
        Assert.Empty(result.Gaps);
    }

    /// <summary>
    /// Confirms duplicate open times with different candle values fail fast.
    /// </summary>
    [Fact]
    public void NormalizeRejectsConflictingDuplicateOpenTime()
    {
        var openTime = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentException>(
            () => CandleNormalizer.Normalize(
                [
                    CreateCandle(openTime, close: 101m),
                    CreateCandle(openTime, close: 102m)
                ],
                Timeframe.Parse("5m")));

        Assert.Contains("Conflicting candles share open time", exception.Message);
    }

    /// <summary>
    /// Confirms missing candle slots are reported as gaps but do not fail normalization.
    /// </summary>
    [Fact]
    public void NormalizeReportsTimeframeGaps()
    {
        var result = CandleNormalizer.Normalize(
            [
                CreateCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero)),
                CreateCandle(new DateTimeOffset(2026, 5, 6, 0, 10, 0, TimeSpan.Zero))
            ],
            Timeframe.Parse("5m"));

        var gap = Assert.Single(result.Gaps);
        Assert.Equal(new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero), gap.ExpectedOpenTimeUtc);
        Assert.Equal(new DateTimeOffset(2026, 5, 6, 0, 10, 0, TimeSpan.Zero), gap.ActualOpenTimeUtc);
        Assert.Equal(1, gap.MissingCandles);
        Assert.Equal(2, result.Candles.Count);
    }

    /// <summary>
    /// Creates a valid historical candle for normalization tests.
    /// </summary>
    /// <param name="openTimeUtc">The UTC open time to assign to the candle.</param>
    /// <param name="close">The close price to assign to the candle.</param>
    /// <returns>The test candle.</returns>
    private static CandleEvent CreateCandle(DateTimeOffset openTimeUtc, decimal close = 101m)
    {
        return new CandleEvent(
            openTimeUtc,
            "BTC_USDT_PERP",
            "binance",
            "5m",
            open: 100m,
            high: 103m,
            low: 99m,
            close: close,
            volume: 12.5m,
            CandleSource.Historical);
    }
}
