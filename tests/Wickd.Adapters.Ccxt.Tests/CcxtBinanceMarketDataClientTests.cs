using ccxt;
using Wickd.Adapters.Ccxt;
using Wickd.Data;
using Wickd.Models;

namespace Wickd.Tests;

/// <summary>
/// Verifies CCXT Binance adapter mapping and OHLCV conversion behavior without live exchange calls.
/// </summary>
public class CcxtBinanceMarketDataClientTests
{
    /// <summary>
    /// Confirms the Wickd Binance exchange ID maps to the CCXT Binance USD-M adapter.
    /// </summary>
    [Fact]
    public void ResolveCcxtExchangeIdMapsBinanceToUsdMFutures()
    {
        var ccxtExchangeId = CcxtBinanceMarketDataClient.ResolveCcxtExchangeId(
            TestSettingsFactory.BinanceMarket);

        Assert.Equal("binanceusdm", ccxtExchangeId);
    }

    /// <summary>
    /// Confirms the Binance client rejects markets from other configured exchanges.
    /// </summary>
    [Fact]
    public void ResolveCcxtExchangeIdRejectsOtherExchange()
    {
        var exception = Assert.Throws<WickdDataException>(
            () => CcxtBinanceMarketDataClient.ResolveCcxtExchangeId(TestSettingsFactory.HyperliquidMarket));

        Assert.Contains("not supported by the Binance CCXT market data client", exception.Message);
    }

    /// <summary>
    /// Confirms CCXT OHLCV values convert to Wickd exchange candles.
    /// </summary>
    [Fact]
    public void ConvertOhlcvPreservesTimestampAndValues()
    {
        var ohlcv = new OHLCV(null!)
        {
            timestamp = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            open = 63812.25,
            high = 63888.5,
            low = 63700.125,
            close = 63850.75,
            volume = 123.5
        };

        var candle = CcxtBinanceMarketDataClient.ConvertOhlcv(ohlcv);

        Assert.Equal(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero), candle.OpenTimeUtc);
        Assert.Equal(63812.25m, candle.Open);
        Assert.Equal(63888.5m, candle.High);
        Assert.Equal(63700.125m, candle.Low);
        Assert.Equal(63850.75m, candle.Close);
        Assert.Equal(123.5m, candle.Volume);
    }

    /// <summary>
    /// Confirms missing CCXT OHLCV fields fail with a clear data exception.
    /// </summary>
    [Fact]
    public void ConvertOhlcvRejectsMissingRequiredFields()
    {
        var ohlcv = new OHLCV(null!)
        {
            timestamp = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            open = 63812.25,
            high = 63888.5,
            low = 63700.125,
            close = 63850.75
        };

        var exception = Assert.Throws<WickdDataException>(
            () => CcxtBinanceMarketDataClient.ConvertOhlcv(ohlcv));

        Assert.Contains("missing volume", exception.Message);
    }

    /// <summary>
    /// Confirms null CCXT OHLCV pages fail as external data failures.
    /// </summary>
    [Fact]
    public void RequireOhlcvPageRejectsNullPage()
    {
        var exception = Assert.Throws<WickdDataException>(
            () => CcxtBinanceMarketDataClient.RequireOhlcvPage(null, TestSettingsFactory.BinanceMarket));

        Assert.Contains("CCXT OHLCV fetch returned null for BTC_USDT_PERP", exception.Message);
    }

    /// <summary>
    /// Confirms huge remaining ranges are clamped before converting to an int page limit.
    /// </summary>
    [Fact]
    public void CountNextFetchLimitClampsHugeRanges()
    {
        var limit = CcxtBinanceMarketDataClient.CountNextFetchLimit(
            sinceMilliseconds: 0,
            endMilliseconds: long.MaxValue,
            timeframeMilliseconds: 60_000);

        Assert.Equal(CcxtBinanceMarketDataClient.FetchLimit, limit);
    }

    /// <summary>
    /// Confirms a partial timeframe still fetches one candle.
    /// </summary>
    [Fact]
    public void CountNextFetchLimitReturnsOneForPartialTimeframe()
    {
        var limit = CcxtBinanceMarketDataClient.CountNextFetchLimit(
            sinceMilliseconds: 0,
            endMilliseconds: 1,
            timeframeMilliseconds: 60_000);

        Assert.Equal(1, limit);
    }

    /// <summary>
    /// Confirms normal ranges round up to the exact number of required candles.
    /// </summary>
    [Fact]
    public void CountNextFetchLimitRoundsUpNormalRanges()
    {
        var limit = CcxtBinanceMarketDataClient.CountNextFetchLimit(
            sinceMilliseconds: 0,
            endMilliseconds: 121_000,
            timeframeMilliseconds: 60_000);

        Assert.Equal(3, limit);
    }
}
