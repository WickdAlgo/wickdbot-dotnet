using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Creates deterministic settings and run requests for WickdBot tests.
/// </summary>
internal static class TestSettingsFactory
{
    /// <summary>
    /// Gets the default Binance market used by tests.
    /// </summary>
    internal static MarketDefinition BinanceMarket { get; } = new(
        "BTC_USDT_PERP",
        "binance",
        "BTC/USDT:USDT");

    /// <summary>
    /// Gets a configured unsupported Hyperliquid market used by tests.
    /// </summary>
    internal static MarketDefinition HyperliquidMarket { get; } = new(
        "BTC_USDC_PERP",
        "hyperliquid",
        "BTC/USDC:USDC");

    /// <summary>
    /// Creates settings with a temporary cache root.
    /// </summary>
    /// <param name="cacheRoot">Cache root to assign to settings.</param>
    /// <param name="markets">Configured markets. Defaults to the Binance market.</param>
    /// <returns>Validated test settings.</returns>
    internal static WickdBotSettings CreateSettings(
        string cacheRoot,
        params MarketDefinition[] markets)
    {
        var configuredMarkets = markets.Length == 0 ? [BinanceMarket] : markets;
        return new WickdBotSettings(
            configuredMarkets[0].MarketId,
            Timeframe.Parse("5m"),
            "markets.json",
            cacheRoot,
            Path.Combine(cacheRoot, "runs"),
            CreateStructureSettings(),
            configuredMarkets.ToDictionary(market => market.MarketId, StringComparer.Ordinal));
    }

    /// <summary>
    /// Creates default structure settings tuned for compact deterministic tests.
    /// </summary>
    /// <returns>Validated structure settings.</returns>
    internal static StructureSettings CreateStructureSettings()
    {
        return new StructureSettings(
            SwingFractalWindow: 1,
            EqualLevelToleranceBasisPoints: 5m,
            OrderBlockSearchBackCandles: 3,
            ExpansionLookbackCandles: 2,
            ExpansionBodyToAverageRange: 1.5m,
            ExpansionFvgWindowCandles: 2);
    }

    /// <summary>
    /// Creates a resolved run request for a configured market.
    /// </summary>
    /// <param name="cacheRoot">Cache root to assign to settings.</param>
    /// <param name="market">Market to request.</param>
    /// <param name="fromUtc">Inclusive UTC start time.</param>
    /// <param name="toUtc">Exclusive UTC end time.</param>
    /// <returns>The resolved run request.</returns>
    internal static RunRequest CreateRunRequest(
        string cacheRoot,
        MarketDefinition? market = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null)
    {
        var requestedMarket = market ?? BinanceMarket;
        return RunRequestFactory.Create(
            CreateSettings(cacheRoot, requestedMarket),
            requestedMarket.MarketId,
            "5m",
            fromUtc ?? new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc ?? new DateTimeOffset(2026, 5, 6, 0, 15, 0, TimeSpan.Zero));
    }
}
