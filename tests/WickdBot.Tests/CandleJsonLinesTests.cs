using WickdBot.Data;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Verifies JSON Lines persistence for normalized candle records.
/// </summary>
public class CandleJsonLinesTests
{
    /// <summary>
    /// Confirms JSONL round-tripping preserves deterministic candle values.
    /// </summary>
    [Fact]
    public async Task RoundTripPreservesCandleValues()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.DirectoryPath, "candles.jsonl");
        var candle = new CandleEvent(
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            "BTC_USDT_PERP",
            "binance",
            "5m",
            open: 63812.12345678m,
            high: 63888.87654321m,
            low: 63700.00000001m,
            close: 63850.55555555m,
            volume: 123.45678901m,
            CandleSource.Backtest,
            runId: "run-000001");

        await CandleJsonLines.WriteAsync(path, [candle]);

        var candles = await CandleJsonLines.ReadAsync(path);
        var actual = Assert.Single(candles);

        Assert.Equal(candle.OpenTimeUtc, actual.OpenTimeUtc);
        Assert.Equal(candle.MarketId, actual.MarketId);
        Assert.Equal(candle.ExchangeId, actual.ExchangeId);
        Assert.Equal(candle.Timeframe, actual.Timeframe);
        Assert.Equal(candle.Open, actual.Open);
        Assert.Equal(candle.High, actual.High);
        Assert.Equal(candle.Low, actual.Low);
        Assert.Equal(candle.Close, actual.Close);
        Assert.Equal(candle.Volume, actual.Volume);
        Assert.Equal(candle.Source, actual.Source);
        Assert.Equal(candle.RunId, actual.RunId);
    }
}
