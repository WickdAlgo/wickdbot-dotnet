#nullable enable

using System.Text.Json.Serialization;

namespace Wickd.Models;

/// <summary>
/// Represents one normalized OHLCV candle with Wickd market metadata.
/// </summary>
public sealed record CandleEvent
{
    /// <summary>
    /// Initializes a normalized candle event.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <param name="marketId">Canonical Wickd market ID.</param>
    /// <param name="exchangeId">Exchange identifier that produced the candle.</param>
    /// <param name="timeframe">Canonical timeframe string.</param>
    /// <param name="open">Open price.</param>
    /// <param name="high">High price.</param>
    /// <param name="low">Low price.</param>
    /// <param name="close">Close price.</param>
    /// <param name="volume">Traded volume.</param>
    /// <param name="source">Pipeline source for the candle.</param>
    /// <param name="runId">Run identifier when the candle belongs to a backtest run.</param>
    /// <exception cref="ArgumentException">Thrown when the candle timestamp is not UTC.</exception>
    [JsonConstructor]
    public CandleEvent(
        DateTimeOffset openTimeUtc,
        string marketId,
        string exchangeId,
        string timeframe,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        CandleSource source,
        string? runId = null)
    {
        if (openTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Candle open time must be expressed in UTC.", nameof(openTimeUtc));
        }

        OpenTimeUtc = openTimeUtc;
        MarketId = marketId;
        ExchangeId = exchangeId;
        Timeframe = timeframe;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        Source = source;
        RunId = runId;
    }

    /// <summary>
    /// Gets the UTC candle open time.
    /// </summary>
    public DateTimeOffset OpenTimeUtc { get; }

    /// <summary>
    /// Gets the canonical Wickd market ID.
    /// </summary>
    public string MarketId { get; }

    /// <summary>
    /// Gets the exchange identifier that produced the candle.
    /// </summary>
    public string ExchangeId { get; }

    /// <summary>
    /// Gets the canonical timeframe string.
    /// </summary>
    public string Timeframe { get; }

    /// <summary>
    /// Gets the open price.
    /// </summary>
    public decimal Open { get; }

    /// <summary>
    /// Gets the high price.
    /// </summary>
    public decimal High { get; }

    /// <summary>
    /// Gets the low price.
    /// </summary>
    public decimal Low { get; }

    /// <summary>
    /// Gets the close price.
    /// </summary>
    public decimal Close { get; }

    /// <summary>
    /// Gets the traded volume.
    /// </summary>
    public decimal Volume { get; }

    /// <summary>
    /// Gets the pipeline source for the candle.
    /// </summary>
    public CandleSource Source { get; }

    /// <summary>
    /// Gets the run identifier when the candle belongs to a backtest run.
    /// </summary>
    public string? RunId { get; }
}
