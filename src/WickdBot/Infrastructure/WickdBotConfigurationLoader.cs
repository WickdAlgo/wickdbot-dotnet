using System.Text.Json;
using System.Text.Json.Nodes;
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
            using var appSettings = LoadMergedAppSettings(appSettingsPath);
            var root = GetRequiredObject(appSettings.RootElement, "WickdBot", "WickdBot");
            var defaults = GetRequiredObject(root, "Defaults", "WickdBot.Defaults");
            var storage = GetRequiredObject(root, "Storage", "WickdBot.Storage");
            var structure = GetRequiredObject(root, "Structure", "WickdBot.Structure");

            var defaultMarketId = GetRequiredString(defaults, "MarketId", "WickdBot.Defaults.MarketId");
            var defaultTimeframe = ParseConfiguredTimeframe(
                GetRequiredString(defaults, "Timeframe", "WickdBot.Defaults.Timeframe"),
                "WickdBot.Defaults.Timeframe");
            var marketsFilePath = GetRequiredString(defaults, "MarketsFilePath", "WickdBot.Defaults.MarketsFilePath");
            var cacheRoot = GetRequiredString(storage, "CacheRoot", "WickdBot.Storage.CacheRoot");
            var runsRoot = GetRequiredString(storage, "RunsRoot", "WickdBot.Storage.RunsRoot");
            var structureSettings = ParseStructureSettings(structure);
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
                structureSettings,
                markets);
        }
        catch (JsonException ex)
        {
            throw new WickdBotConfigurationException($"Configuration JSON is invalid: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads appsettings.json and applies an optional appsettings.Local.json override from the same directory.
    /// </summary>
    /// <param name="appSettingsPath">Path to the committed appsettings.json file.</param>
    /// <returns>The merged configuration document.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when either settings document is not a JSON object.</exception>
    private static JsonDocument LoadMergedAppSettings(string appSettingsPath)
    {
        var root = LoadAppSettingsObject(appSettingsPath);
        var localPath = BuildLocalAppSettingsPath(appSettingsPath);
        if (File.Exists(localPath))
        {
            var localRoot = LoadAppSettingsObject(localPath);
            MergeObject(root, localRoot);
        }

        return JsonDocument.Parse(root.ToJsonString());
    }

    /// <summary>
    /// Loads a settings document as a mutable JSON object.
    /// </summary>
    /// <param name="path">Settings file path.</param>
    /// <returns>The root JSON object.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the settings root is not a JSON object.</exception>
    private static JsonObject LoadAppSettingsObject(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new WickdBotConfigurationException($"Configuration JSON is empty: {path}");
        if (node is JsonObject jsonObject)
        {
            return jsonObject;
        }

        throw new WickdBotConfigurationException($"Configuration root must be an object: {path}");
    }

    /// <summary>
    /// Builds the optional local override path for a settings file.
    /// </summary>
    /// <param name="appSettingsPath">Path to the committed appsettings.json file.</param>
    /// <returns>The expected local override path.</returns>
    private static string BuildLocalAppSettingsPath(string appSettingsPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(appSettingsPath))
            ?? throw new WickdBotConfigurationException($"Cannot resolve directory for {appSettingsPath}.");

        return Path.Combine(directory, "appsettings.Local.json");
    }

    /// <summary>
    /// Recursively overlays local settings onto committed settings.
    /// </summary>
    /// <param name="target">Base settings object to mutate.</param>
    /// <param name="source">Override settings object.</param>
    private static void MergeObject(JsonObject target, JsonObject source)
    {
        foreach (var property in source.ToArray())
        {
            if (property.Value is JsonObject sourceObject
                && target[property.Key] is JsonObject targetObject)
            {
                MergeObject(targetObject, sourceObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    /// <summary>
    /// Loads and validates configured market definitions from markets.json.
    /// </summary>
    /// <param name="marketsFilePath">The resolved path to markets.json.</param>
    /// <returns>Market definitions keyed by canonical market ID.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the markets file is missing or invalid.</exception>
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

    /// <summary>
    /// Parses and validates structure-engine configuration.
    /// </summary>
    /// <param name="structure">The WickdBot.Structure object.</param>
    /// <returns>Validated structure settings.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when a required value is missing or invalid.</exception>
    private static StructureSettings ParseStructureSettings(JsonElement structure)
    {
        var minimumSwingSeparationCandles = GetRequiredInt32(
            structure,
            "MinimumSwingSeparationCandles",
            "WickdBot.Structure.MinimumSwingSeparationCandles");
        var equalLevelToleranceBasisPoints = GetRequiredDecimal(
            structure,
            "EqualLevelToleranceBasisPoints",
            "WickdBot.Structure.EqualLevelToleranceBasisPoints");
        var orderBlockSearchBackCandles = GetRequiredInt32(
            structure,
            "OrderBlockSearchBackCandles",
            "WickdBot.Structure.OrderBlockSearchBackCandles");
        var expansionLookbackCandles = GetRequiredInt32(
            structure,
            "ExpansionLookbackCandles",
            "WickdBot.Structure.ExpansionLookbackCandles");
        var expansionBodyToAverageRange = GetRequiredDecimal(
            structure,
            "ExpansionBodyToAverageRange",
            "WickdBot.Structure.ExpansionBodyToAverageRange");
        var expansionFvgWindowCandles = GetRequiredInt32(
            structure,
            "ExpansionFvgWindowCandles",
            "WickdBot.Structure.ExpansionFvgWindowCandles");

        EnsureAtLeast(minimumSwingSeparationCandles, 1, "WickdBot.Structure.MinimumSwingSeparationCandles");
        EnsureAtLeast(equalLevelToleranceBasisPoints, 0m, "WickdBot.Structure.EqualLevelToleranceBasisPoints");
        EnsureAtLeast(orderBlockSearchBackCandles, 1, "WickdBot.Structure.OrderBlockSearchBackCandles");
        EnsureAtLeast(expansionLookbackCandles, 1, "WickdBot.Structure.ExpansionLookbackCandles");
        EnsureGreaterThanZero(expansionBodyToAverageRange, "WickdBot.Structure.ExpansionBodyToAverageRange");
        EnsureAtLeast(expansionFvgWindowCandles, 1, "WickdBot.Structure.ExpansionFvgWindowCandles");

        return new StructureSettings(
            minimumSwingSeparationCandles,
            equalLevelToleranceBasisPoints,
            orderBlockSearchBackCandles,
            expansionLookbackCandles,
            expansionBodyToAverageRange,
            expansionFvgWindowCandles);
    }

    /// <summary>
    /// Finds appsettings.json for either normal application execution or test execution from the repository root.
    /// </summary>
    /// <returns>The resolved appsettings.json path.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when no default settings file can be found.</exception>
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

    /// <summary>
    /// Resolves a configured file path relative to the file that referenced it.
    /// </summary>
    /// <param name="anchorPath">The file path used as the relative-path anchor.</param>
    /// <param name="candidatePath">The configured absolute or relative path.</param>
    /// <returns>The resolved path.</returns>
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

    /// <summary>
    /// Parses a configured timeframe and reports invalid values as configuration failures.
    /// </summary>
    /// <param name="value">The configured timeframe value.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The parsed timeframe.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the timeframe is not supported.</exception>
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

    /// <summary>
    /// Reads a required JSON object property.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required object value.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the property is missing or is not an object.</exception>
    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be an object.");
        }

        return property;
    }

    /// <summary>
    /// Reads a required JSON array property.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required array value.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the property is missing or is not an array.</exception>
    private static JsonElement GetRequiredArray(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be an array.");
        }

        return property;
    }

    /// <summary>
    /// Reads a required non-empty JSON string property.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required string value.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the property is missing, not a string, or empty.</exception>
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

    /// <summary>
    /// Reads a required integer JSON number property.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required integer value.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the property is missing or is not an integer.</exception>
    private static int GetRequiredInt32(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be an integer.");
        }

        return value;
    }

    /// <summary>
    /// Reads a required decimal JSON number property.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required decimal value.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the property is missing or is not numeric.</exception>
    private static decimal GetRequiredDecimal(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var value))
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' must be a number.");
        }

        return value;
    }

    /// <summary>
    /// Validates that an integer configuration value is at least the supplied minimum.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minimum">Inclusive minimum value.</param>
    /// <param name="path">Configuration path used in error messages.</param>
    /// <exception cref="WickdBotConfigurationException">Thrown when the value is too small.</exception>
    private static void EnsureAtLeast(int value, int minimum, string path)
    {
        if (value < minimum)
        {
            throw new WickdBotConfigurationException(
                $"Configuration value '{path}' must be at least {minimum}.");
        }
    }

    /// <summary>
    /// Validates that a decimal configuration value is at least the supplied minimum.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minimum">Inclusive minimum value.</param>
    /// <param name="path">Configuration path used in error messages.</param>
    /// <exception cref="WickdBotConfigurationException">Thrown when the value is too small.</exception>
    private static void EnsureAtLeast(decimal value, decimal minimum, string path)
    {
        if (value < minimum)
        {
            throw new WickdBotConfigurationException(
                $"Configuration value '{path}' must be at least {minimum}.");
        }
    }

    /// <summary>
    /// Validates that a decimal configuration value is positive.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="path">Configuration path used in error messages.</param>
    /// <exception cref="WickdBotConfigurationException">Thrown when the value is zero or negative.</exception>
    private static void EnsureGreaterThanZero(decimal value, string path)
    {
        if (value <= 0m)
        {
            throw new WickdBotConfigurationException(
                $"Configuration value '{path}' must be greater than 0.");
        }
    }

    /// <summary>
    /// Reads a required JSON property without validating its value kind.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required property value.</returns>
    /// <exception cref="WickdBotConfigurationException">Thrown when the property is missing.</exception>
    private static JsonElement GetRequiredProperty(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new WickdBotConfigurationException($"Configuration value '{path}' is required.");
        }

        return property;
    }
}
