#nullable enable

namespace WickdBot.Data;

/// <summary>
/// Represents one raw historical OHLCV candle returned by a market data adapter.
/// </summary>
internal sealed record ExchangeCandle
{
    /// <summary>
    /// Initializes a raw exchange candle and validates its UTC timestamp.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <param name="open">Open price.</param>
    /// <param name="high">High price.</param>
    /// <param name="low">Low price.</param>
    /// <param name="close">Close price.</param>
    /// <param name="volume">Traded volume.</param>
    /// <exception cref="ArgumentException">Thrown when the candle timestamp is not UTC.</exception>
    internal ExchangeCandle(
        DateTimeOffset openTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume)
    {
        if (openTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Exchange candle open time must be expressed in UTC.", nameof(openTimeUtc));
        }

        OpenTimeUtc = openTimeUtc;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    /// <summary>
    /// Gets the UTC candle open time.
    /// </summary>
    internal DateTimeOffset OpenTimeUtc { get; }

    /// <summary>
    /// Gets the open price.
    /// </summary>
    internal decimal Open { get; }

    /// <summary>
    /// Gets the high price.
    /// </summary>
    internal decimal High { get; }

    /// <summary>
    /// Gets the low price.
    /// </summary>
    internal decimal Low { get; }

    /// <summary>
    /// Gets the close price.
    /// </summary>
    internal decimal Close { get; }

    /// <summary>
    /// Gets the traded volume.
    /// </summary>
    internal decimal Volume { get; }
}
