using WickdBot.Data;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Deterministic market data client used by tests instead of live exchange calls.
/// </summary>
internal sealed class FakeMarketDataClient : IMarketDataClient
{
    /// <summary>
    /// Fetch delegate used to produce exchange candles.
    /// </summary>
    private readonly Func<MarketDefinition, Timeframe, DateRange, CancellationToken, Task<IReadOnlyList<ExchangeCandle>>> fetch;

    /// <summary>
    /// Initializes a fake market data client.
    /// </summary>
    /// <param name="exchangeId">Supported WickdBot exchange ID.</param>
    /// <param name="fetch">Fetch delegate used to produce exchange candles.</param>
    internal FakeMarketDataClient(
        string exchangeId,
        Func<MarketDefinition, Timeframe, DateRange, CancellationToken, Task<IReadOnlyList<ExchangeCandle>>> fetch)
    {
        ExchangeId = exchangeId;
        this.fetch = fetch;
    }

    /// <inheritdoc />
    public string ExchangeId { get; }

    /// <summary>
    /// Gets the number of times candles have been fetched.
    /// </summary>
    internal int FetchCount { get; private set; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExchangeCandle>> FetchCandlesAsync(
        MarketDefinition market,
        Timeframe timeframe,
        DateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        FetchCount++;
        return await fetch(market, timeframe, dateRange, cancellationToken);
    }
}
