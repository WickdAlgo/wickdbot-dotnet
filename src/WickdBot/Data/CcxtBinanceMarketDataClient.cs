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
    private const int FetchLimit = 1000;

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

            var remainingCandles = CountRemainingCandles(
                sinceMilliseconds,
                requestEndMilliseconds,
                timeframeMilliseconds);
            var limit = Math.Min(FetchLimit, remainingCandles);
            var ohlcvs = await exchange.FetchOHLCV(
                market.ExchangeSymbol,
                timeframe.Value,
                sinceMilliseconds,
                limit);

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
    /// Counts how many candle opens are still needed for a paged request.
    /// </summary>
    /// <param name="sinceMilliseconds">Current inclusive fetch timestamp in Unix milliseconds.</param>
    /// <param name="endMilliseconds">Exclusive request end timestamp in Unix milliseconds.</param>
    /// <param name="timeframeMilliseconds">Requested timeframe in milliseconds.</param>
    /// <returns>The remaining candle count.</returns>
    private static int CountRemainingCandles(
        long sinceMilliseconds,
        long endMilliseconds,
        long timeframeMilliseconds)
    {
        var remainingMilliseconds = endMilliseconds - sinceMilliseconds;
        return Math.Max(1, (int)Math.Ceiling(remainingMilliseconds / (double)timeframeMilliseconds));
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
