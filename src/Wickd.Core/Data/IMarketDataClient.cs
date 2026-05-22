#nullable enable

using Wickd.Models;

namespace Wickd.Data;

/// <summary>
/// Fetches historical OHLCV candles from one configured market data exchange.
/// </summary>
public interface IMarketDataClient
{
    /// <summary>
    /// Gets the Wickd exchange ID supported by this client.
    /// </summary>
    public string ExchangeId { get; }

    /// <summary>
    /// Fetches historical candles for a single market, timeframe, and UTC range.
    /// </summary>
    /// <param name="market">Configured market identity.</param>
    /// <param name="timeframe">Requested candle timeframe.</param>
    /// <param name="dateRange">Requested UTC date range.</param>
    /// <param name="cancellationToken">Cancellation token for the fetch operation.</param>
    /// <returns>Exchange candles in adapter-provided order.</returns>
    /// <exception cref="WickdDataException">Thrown when the exchange request or returned candle data is invalid.</exception>
    public Task<IReadOnlyList<ExchangeCandle>> FetchCandlesAsync(
        MarketDefinition market,
        Timeframe timeframe,
        DateRange dateRange,
        CancellationToken cancellationToken = default);
}
