using Wickd.Models;

namespace Wickd.Infrastructure;

/// <summary>
/// Represents validated Wickd settings loaded from appsettings.json and markets.json.
/// </summary>
/// <param name="DefaultMarketId">Default canonical market ID.</param>
/// <param name="DefaultTimeframe">Default candle timeframe.</param>
/// <param name="MarketsFilePath">Resolved markets.json path.</param>
/// <param name="CacheRoot">Root folder for reusable candle cache files.</param>
/// <param name="RunsRoot">Root folder for run-specific outputs.</param>
/// <param name="Structure">Validated market-structure detection parameters.</param>
/// <param name="Markets">Validated market definitions keyed by canonical market ID.</param>
internal sealed record WickdSettings(
    string DefaultMarketId,
    Timeframe DefaultTimeframe,
    string MarketsFilePath,
    string CacheRoot,
    string RunsRoot,
    StructureSettings Structure,
    IReadOnlyDictionary<string, MarketDefinition> Markets)
{
    /// <summary>
    /// Resolves a configured market by canonical market ID.
    /// </summary>
    /// <param name="marketId">The requested canonical market ID.</param>
    /// <returns>The resolved market definition.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when the market is not configured.</exception>
    internal MarketDefinition ResolveMarket(string marketId)
    {
        if (string.IsNullOrWhiteSpace(marketId))
        {
            throw new WickdConfigurationException("Market ID is required.");
        }

        if (Markets.TryGetValue(marketId, out var market))
        {
            return market;
        }

        throw new WickdConfigurationException($"Unknown market '{marketId}'.");
    }
}
