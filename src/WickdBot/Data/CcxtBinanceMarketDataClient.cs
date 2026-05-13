#nullable enable

using System.Globalization;
using ccxt;
using WickdBot.Models;

namespace WickdBot.Data;

/// <summary>
/// Fetches Binance USD-M historical candles through CCXT while preserving WickdBot's Binance exchange identity.
/// </summary>
internal sealed class CcxtBinanceMarketDataClient : IMarketDataClient
{
    /// <summary>
    /// WickdBot exchange ID supported by this client.
    /// </summary>
    internal const string WickdBotExchangeId = "binance";

    /// <summary>
    /// CCXT exchange ID used for Binance USDT perpetual markets.
    /// </summary>
    internal const string CcxtExchangeId = "binanceusdm";

    /// <summary>
    /// Maximum OHLCV candles requested per CCXT page.
    /// </summary>
    internal const int FetchLimit = 1000;

    /// <inheritdoc />
    public string ExchangeId => WickdBotExchangeId;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExchangeCandle>> FetchCandlesAsync(
        MarketDefinition market,
        Timeframe timeframe,
        DateRange dateRange,
        CancellationToken cancellationToken = default)
    {
        _ = ResolveCcxtExchangeId(market);
        var exchange = new Binanceusdm(new Dictionary<string, object>
        {
            ["enableRateLimit"] = true
        });

        try
        {
            return await FetchCandlesAsync(exchange, market, timeframe, dateRange, cancellationToken);
        }
        catch (Exception ex) when (ex is not WickdBotDataException and not OperationCanceledException)
        {
            throw new WickdBotDataException(
                $"CCXT historical fetch failed for {market.MarketId} on {CcxtExchangeId}: {ex.Message}",
                ex);
        }
        finally
        {
            await exchange.Close();
        }
    }

    /// <summary>
    /// Resolves the CCXT adapter ID for a configured WickdBot Binance market.
    /// </summary>
    /// <param name="market">Configured market identity.</param>
    /// <returns>The CCXT exchange ID.</returns>
    /// <exception cref="WickdBotDataException">Thrown when the market is not supported by this client.</exception>
    internal static string ResolveCcxtExchangeId(MarketDefinition market)
    {
        if (market.ExchangeId != WickdBotExchangeId)
        {
            throw new WickdBotDataException(
                $"Exchange '{market.ExchangeId}' is not supported by the Binance CCXT market data client.");
        }

        return CcxtExchangeId;
    }

    /// <summary>
    /// Converts a CCXT OHLCV record into WickdBot's exchange candle contract.
    /// </summary>
    /// <param name="ohlcv">CCXT OHLCV record.</param>
    /// <returns>The converted exchange candle.</returns>
    /// <exception cref="WickdBotDataException">Thrown when CCXT omits required OHLCV fields.</exception>
    internal static ExchangeCandle ConvertOhlcv(OHLCV ohlcv)
    {
        if (ohlcv.timestamp is null)
        {
            throw new WickdBotDataException("CCXT OHLCV candle is missing timestamp.");
        }

        return new ExchangeCandle(
            DateTimeOffset.FromUnixTimeMilliseconds(ohlcv.timestamp.Value),
            ConvertRequiredDecimal(ohlcv.open, "open"),
            ConvertRequiredDecimal(ohlcv.high, "high"),
            ConvertRequiredDecimal(ohlcv.low, "low"),
            ConvertRequiredDecimal(ohlcv.close, "close"),
            ConvertRequiredDecimal(ohlcv.volume, "volume"));
    }

    /// <summary>
    /// Fetches one or more CCXT pages and filters candles to the requested range.
    /// </summary>
    /// <param name="exchange">CCXT Binance USD-M exchange instance.</param>
    /// <param name="market">Configured market identity.</param>
    /// <param name="timeframe">Requested candle timeframe.</param>
    /// <param name="dateRange">Requested UTC date range.</param>
    /// <param name="cancellationToken">Cancellation token for the fetch operation.</param>
    /// <returns>Fetched exchange candles inside the requested range.</returns>
    private static async Task<IReadOnlyList<ExchangeCandle>> FetchCandlesAsync(
        Binanceusdm exchange,
        MarketDefinition market,
        Timeframe timeframe,
        DateRange dateRange,
        CancellationToken cancellationToken)
    {
        var candles = new List<ExchangeCandle>();
        var timeframeMilliseconds = checked((long)timeframe.Duration.TotalMilliseconds);
        var requestEndMilliseconds = dateRange.ToUtc.ToUnixTimeMilliseconds();
        var sinceMilliseconds = dateRange.FromUtc.ToUnixTimeMilliseconds();

        while (sinceMilliseconds < requestEndMilliseconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var limit = CountNextFetchLimit(
                sinceMilliseconds,
                requestEndMilliseconds,
                timeframeMilliseconds);
            var ohlcvs = RequireOhlcvPage(
                await exchange.FetchOHLCV(
                    market.ExchangeSymbol,
                    timeframe.Value,
                    sinceMilliseconds,
                    limit),
                market);

            cancellationToken.ThrowIfCancellationRequested();

            if (ohlcvs.Count == 0)
            {
                break;
            }

            long? latestTimestampMilliseconds = null;
            foreach (var ohlcv in ohlcvs)
            {
                var candle = ConvertOhlcv(ohlcv);
                var openMilliseconds = candle.OpenTimeUtc.ToUnixTimeMilliseconds();
                if (latestTimestampMilliseconds is null || openMilliseconds > latestTimestampMilliseconds)
                {
                    latestTimestampMilliseconds = openMilliseconds;
                }

                if (candle.OpenTimeUtc >= dateRange.FromUtc && candle.OpenTimeUtc < dateRange.ToUtc)
                {
                    candles.Add(candle);
                }
            }

            if (latestTimestampMilliseconds is null)
            {
                break;
            }

            var nextSinceMilliseconds = latestTimestampMilliseconds.Value + timeframeMilliseconds;
            if (nextSinceMilliseconds <= sinceMilliseconds)
            {
                throw new WickdBotDataException(
                    $"CCXT returned non-advancing OHLCV timestamps at {latestTimestampMilliseconds.Value}.");
            }

            sinceMilliseconds = nextSinceMilliseconds;
            if (ohlcvs.Count < limit)
            {
                break;
            }
        }

        return candles;
    }

    /// <summary>
    /// Validates that CCXT returned a concrete OHLCV page.
    /// </summary>
    /// <param name="ohlcvs">OHLCV page returned by CCXT.</param>
    /// <param name="market">Configured market identity.</param>
    /// <returns>The validated OHLCV page.</returns>
    /// <exception cref="WickdBotDataException">Thrown when CCXT returns a null page.</exception>
    internal static IReadOnlyCollection<OHLCV> RequireOhlcvPage(
        IReadOnlyCollection<OHLCV>? ohlcvs,
        MarketDefinition market)
    {
        if (ohlcvs is null)
        {
            throw new WickdBotDataException(
                $"CCXT OHLCV fetch returned null for {market.MarketId}.");
        }

        return ohlcvs;
    }

    /// <summary>
    /// Counts the next CCXT fetch page limit for a paged request.
    /// </summary>
    /// <param name="sinceMilliseconds">Current inclusive fetch timestamp in Unix milliseconds.</param>
    /// <param name="endMilliseconds">Exclusive request end timestamp in Unix milliseconds.</param>
    /// <param name="timeframeMilliseconds">Requested timeframe in milliseconds.</param>
    /// <returns>A page limit between 1 and <see cref="FetchLimit" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the timeframe duration is not positive.</exception>
    internal static int CountNextFetchLimit(
        long sinceMilliseconds,
        long endMilliseconds,
        long timeframeMilliseconds)
    {
        if (timeframeMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeframeMilliseconds),
                "Timeframe duration must be positive.");
        }

        var remainingMilliseconds = endMilliseconds - sinceMilliseconds;
        if (remainingMilliseconds <= 0)
        {
            return 1;
        }

        var wholeCandles = remainingMilliseconds / timeframeMilliseconds;
        var includesPartialCandle = remainingMilliseconds % timeframeMilliseconds != 0;
        var requestedCandles = wholeCandles + (includesPartialCandle ? 1 : 0);
        if (requestedCandles <= 1)
        {
            return 1;
        }

        return requestedCandles > FetchLimit ? FetchLimit : (int)requestedCandles;
    }

    /// <summary>
    /// Converts a required nullable CCXT double field into a decimal value.
    /// </summary>
    /// <param name="value">CCXT numeric field value.</param>
    /// <param name="fieldName">Field name for diagnostics.</param>
    /// <returns>The decimal value.</returns>
    /// <exception cref="WickdBotDataException">Thrown when the field is missing or non-finite.</exception>
    private static decimal ConvertRequiredDecimal(double? value, string fieldName)
    {
        if (value is null)
        {
            throw new WickdBotDataException($"CCXT OHLCV candle is missing {fieldName}.");
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            throw new WickdBotDataException($"CCXT OHLCV candle has non-finite {fieldName}.");
        }

        return Convert.ToDecimal(value.Value, CultureInfo.InvariantCulture);
    }
}
