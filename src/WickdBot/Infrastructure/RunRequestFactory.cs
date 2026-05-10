using WickdBot.Models;

namespace WickdBot.Infrastructure;

/// <summary>
/// Resolves command-line arguments into validated WickdBot run requests.
/// </summary>
internal static class RunRequestFactory
{
    /// <summary>
    /// Creates a validated run request from command arguments and settings.
    /// </summary>
    /// <param name="settings">Loaded WickdBot settings.</param>
    /// <param name="marketId">Requested canonical market ID.</param>
    /// <param name="timeframeValue">Requested candle timeframe.</param>
    /// <param name="fromUtc">Inclusive UTC start time.</param>
    /// <param name="toUtc">Exclusive UTC end time.</param>
    /// <returns>The resolved run request.</returns>
    internal static RunRequest Create(
        WickdBotSettings settings,
        string marketId,
        string timeframeValue,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        var market = settings.ResolveMarket(marketId);
        var timeframe = Timeframe.Parse(timeframeValue);
        var dateRange = new DateRange(fromUtc, toUtc);
        var cachePath = BuildCandleCachePath(settings.CacheRoot, market, timeframe, dateRange);

        return new RunRequest(market, timeframe, dateRange, cachePath);
    }

    /// <summary>
    /// Builds the deterministic JSONL candle cache path for a request.
    /// </summary>
    /// <param name="cacheRoot">Configured candle cache root.</param>
    /// <param name="market">Resolved market definition.</param>
    /// <param name="timeframe">Validated timeframe.</param>
    /// <param name="dateRange">Validated UTC date range.</param>
    /// <returns>The candle cache path.</returns>
    internal static string BuildCandleCachePath(
        string cacheRoot,
        MarketDefinition market,
        Timeframe timeframe,
        DateRange dateRange)
    {
        var dateSegment = FormattableString.Invariant(
            $"{FormatCacheInstant(dateRange.FromUtc)}_{FormatCacheInstant(dateRange.ToUtc)}");

        return Path.Combine(
            cacheRoot,
            market.ExchangeId,
            market.MarketId,
            timeframe.Value,
            dateSegment,
            "candles.jsonl");
    }

    /// <summary>
    /// Formats a UTC instant for use in a deterministic, filesystem-safe cache path segment.
    /// </summary>
    /// <param name="instantUtc">The UTC instant to format.</param>
    /// <returns>The cache path representation of the instant.</returns>
    private static string FormatCacheInstant(DateTimeOffset instantUtc)
    {
        return FormattableString.Invariant($"{instantUtc:yyyy-MM-dd'T'HH-mm-ss.fffffff'Z'}");
    }
}
