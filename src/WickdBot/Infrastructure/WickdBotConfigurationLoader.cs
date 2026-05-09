using System.Text.Json;
using WickdBot.Models;

namespace WickdBot.Infrastructure;

/// <summary>
/// Loads and validates WickdBot configuration from appsettings.json and markets.json.
/// </summary>
internal static class WickdBotConfigurationLoader
{
    /// <summary>
    /// Loads configuration from the default appsettings.json location.
    /// </summary>
    /// <returns>The validated WickdBot settings.</returns>
    internal static WickdBotSettings LoadDefault()
    {
        return Load(FindDefaultAppSettingsPath());
    }

    /// <summary>
    /// Loads configuration from a specific appsettings.json file.
    /// </summary>
    /// <param name="appSettingsPath">Path to appsettings.json.</param>
    /// <returns>The validated WickdBot settings.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when configuration is missing or invalid.</exception>
    internal static WickdBotSettings Load(string appSettingsPath)
    {
        if (string.IsNullOrWhiteSpace(appSettingsPath))
        {
            throw new WickdBotConfigurationException("App settings path is required.");
        }

        if (!File.Exists(appSettingsPath))
        {
            throw new WickdBotConfigurationException($"App settings file was not found: {appSettingsPath}");
        }

        try
        {
            using var appSettings = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
            var root = GetRequiredObject(appSettings.RootElement, "WickdBot", "WickdBot");
            var defaults = GetRequiredObject(root, "Defaults", "WickdBot.Defaults");
            var storage = GetRequiredObject(root, "Storage", "WickdBot.Storage");

            var defaultMarketId = GetRequiredString(defaults, "MarketId", "WickdBot.Defaults.MarketId");
            var defaultTimeframe = ParseConfiguredTimeframe(
                GetRequiredString(defaults, "Timeframe", "WickdBot.Defaults.Timeframe"),
                "WickdBot.Defaults.Timeframe");
            var marketsFilePath = GetRequiredString(defaults, "MarketsFilePath", "WickdBot.Defaults.MarketsFilePath");
            var cacheRoot = GetRequiredString(storage, "CacheRoot", "WickdBot.Storage.CacheRoot");
            var runsRoot = GetRequiredString(storage, "RunsRoot", "WickdBot.Storage.RunsRoot");
            var resolvedMarketsPath = ResolvePathRelativeTo(appSettingsPath, marketsFilePath);
            var markets = LoadMarkets(resolvedMarketsPath);

            if (!markets.ContainsKey(defaultMarketId))
            {
                throw new WickdBotConfigurationException($"Default market '{defaultMarketId}' is not configured in markets.json.");
            }

            return new WickdBotSettings(
                defaultMarketId,
                defaultTimeframe,
                resolvedMarketsPath,
                cacheRoot,
                runsRoot,
                markets);
        }
        catch (JsonException ex)
        {
            throw new WickdBotConfigurationException($"Configuration JSON is invalid: {ex.Message}", ex);
        }
    }

    private static IReadOnlyDictionary<string, MarketDefinition> LoadMarkets(string marketsFilePath)
    {
        if (!File.Exists(marketsFilePath))
        {
            throw new WickdBotConfigurationException($"Markets file was not found: {marketsFilePath}");
        }

        using var marketsDocument = JsonDocument.Parse(File.ReadAllText(marketsFilePath));
        var marketsElement = GetRequiredArray(marketsDocument.RootElement, "Markets", "Markets");
        var markets = new Dictionary<string, MarketDefinition>(StringComparer.Ordinal);

        foreach (var marketElement in marketsElement.EnumerateArray())
        {
            var marketId = GetRequiredString(marketElement, "MarketId", "Markets[].MarketId");
            var exchangeId = GetRequiredString(marketElement, "ExchangeId", "Markets[].ExchangeId");
            var exchangeSymbol = GetRequiredString(marketElement, "ExchangeSymbol", "Markets[].ExchangeSymbol");

            if (markets.ContainsKey(marketId))
            {
                throw new WickdBotConfigurationException($"Duplicate market ID '{marketId}' in markets.json.");
            }

            markets.Add(marketId, new MarketDefinition(marketId, exchangeId, exchangeSymbol));
        }

        if (markets.Count == 0)
        {
            throw new WickdBotConfigurationException("At least one market must be configured in markets.json.");
        }

        return markets;
    }

    private static string FindDefaultAppSettingsPath()
    {
        var baseDirectoryCandidate = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(baseDirectoryCandidate))
        {
            return baseDirectoryCandidate;
        }

        var currentDirectoryCandidate = Path.Combine(Environment.CurrentDirectory, "src", "WickdBot", "appsettings.json");
        if (File.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        throw new WickdBotConfigurationException(
            $"App settings file was not found. Checked '{baseDirectoryCandidate}' and '{currentDirectoryCandidate}'.");
    }

    private static string ResolvePathRelativeTo(string anchorPath, string candidatePath)
    {
        if (Path.IsPathRooted(candidatePath))
        {
            return candidatePath;
        }

        var anchorDirectory = Path.GetDirectoryName(Path.GetFullPath(anchorPath))
            ?? throw new WickdBotConfigurationException($"Cannot resolve directory for {anchorPath}.");

        return Path.Combine(anchorDirectory, candidatePath);
    }

    private static Timeframe ParseConfiguredTimeframe(string value, string path)
    {
        try
        {
            return Timeframe.Parse(value);
        }
        catch (ArgumentException ex)
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' is invalid: {ex.Message}", ex);
        }
    }

    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be an object.");
        }

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be an array.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.String)
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' is required.");
        }

        return value;
    }

    private static JsonElement GetRequiredProperty(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' is required.");
        }

        return property;
    }
}
