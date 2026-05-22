#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using Wickd.Models;

namespace Wickd.Infrastructure;

/// <summary>
/// Loads and validates Wickd configuration from appsettings.json and markets.json.
/// </summary>
internal static class WickdConfigurationLoader
{
    /// <summary>
    /// Environment variable that can point Wickd to a specific appsettings.json file.
    /// </summary>
    internal const string ConfigEnvironmentVariableName = "WICKD_CONFIG";

    private const string AppSettingsFileName = "appsettings.json";
    private const string MarketsFileName = "markets.json";
    private const string AppSettingsDefaultsFileName = "appsettings.defaults.json";
    private const string MarketsDefaultsFileName = "markets.defaults.json";

    /// <summary>
    /// Loads configuration from the resolved default appsettings.json location.
    /// </summary>
    /// <returns>The validated Wickd settings.</returns>
    internal static WickdSettings LoadDefault()
    {
        return LoadResolved(explicitAppSettingsPath: null);
    }

    /// <summary>
    /// Loads configuration using command-line, environment, or user-profile resolution.
    /// </summary>
    /// <param name="explicitAppSettingsPath">Optional command-line appsettings.json path.</param>
    /// <returns>The validated Wickd settings.</returns>
    internal static WickdSettings LoadResolved(string? explicitAppSettingsPath)
    {
        return Load(ResolveAppSettingsPath(explicitAppSettingsPath));
    }

    /// <summary>
    /// Loads configuration from a specific appsettings.json file.
    /// </summary>
    /// <param name="appSettingsPath">Path to appsettings.json.</param>
    /// <returns>The validated Wickd settings.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when configuration is missing or invalid.</exception>
    internal static WickdSettings Load(string appSettingsPath)
    {
        if (string.IsNullOrWhiteSpace(appSettingsPath))
        {
            throw new WickdConfigurationException("App settings path is required.");
        }

        if (!File.Exists(appSettingsPath))
        {
            throw new WickdConfigurationException($"App settings file was not found: {appSettingsPath}");
        }

        try
        {
            using var appSettings = LoadMergedAppSettings(appSettingsPath);
            var root = GetRequiredObject(appSettings.RootElement, "Wickd", "Wickd");
            var defaults = GetRequiredObject(root, "Defaults", "Wickd.Defaults");
            var storage = GetRequiredObject(root, "Storage", "Wickd.Storage");
            var structure = GetRequiredObject(root, "Structure", "Wickd.Structure");

            var defaultMarketId = GetRequiredString(defaults, "MarketId", "Wickd.Defaults.MarketId");
            var defaultTimeframe = ParseConfiguredTimeframe(
                GetRequiredString(defaults, "Timeframe", "Wickd.Defaults.Timeframe"),
                "Wickd.Defaults.Timeframe");
            var marketsFilePath = GetRequiredString(defaults, "MarketsFilePath", "Wickd.Defaults.MarketsFilePath");
            var cacheRoot = GetRequiredString(storage, "CacheRoot", "Wickd.Storage.CacheRoot");
            var runsRoot = GetRequiredString(storage, "RunsRoot", "Wickd.Storage.RunsRoot");
            var structureSettings = ParseStructureSettings(structure);
            var resolvedMarketsPath = ResolvePathRelativeTo(appSettingsPath, marketsFilePath);
            var markets = LoadMarkets(resolvedMarketsPath);

            if (!markets.ContainsKey(defaultMarketId))
            {
                throw new WickdConfigurationException($"Default market '{defaultMarketId}' is not configured in markets.json.");
            }

            return new WickdSettings(
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
            throw new WickdConfigurationException($"Configuration JSON is invalid: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Resolves the appsettings path using command-line, environment, and user-profile defaults.
    /// </summary>
    /// <param name="explicitAppSettingsPath">Optional command-line appsettings.json path.</param>
    /// <returns>The resolved appsettings path.</returns>
    internal static string ResolveAppSettingsPath(string? explicitAppSettingsPath)
    {
        return ResolveAppSettingsPath(explicitAppSettingsPath, GetDefaultUserConfigurationDirectory());
    }

    /// <summary>
    /// Resolves the appsettings path using command-line, environment, and supplied user-profile defaults.
    /// </summary>
    /// <param name="explicitAppSettingsPath">Optional command-line appsettings.json path.</param>
    /// <param name="userConfigurationDirectory">User configuration directory to use when no override is supplied.</param>
    /// <returns>The resolved appsettings path.</returns>
    internal static string ResolveAppSettingsPath(string? explicitAppSettingsPath, string userConfigurationDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitAppSettingsPath))
        {
            return Path.GetFullPath(explicitAppSettingsPath);
        }

        var environmentPath = Environment.GetEnvironmentVariable(ConfigEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return Path.GetFullPath(environmentPath);
        }

        var userPaths = GetConfigurationPaths(userConfigurationDirectory, AppSettingsFileName);
        if (!File.Exists(userPaths.AppSettingsPath))
        {
            _ = InitializeUserConfiguration(userPaths.ConfigDirectory, overwrite: false);
        }

        return userPaths.AppSettingsPath;
    }

    /// <summary>
    /// Gets the default user-profile configuration paths for the current operating system.
    /// </summary>
    /// <returns>The resolved user configuration paths.</returns>
    internal static WickdConfigurationPaths GetUserConfigurationPaths()
    {
        return GetConfigurationPaths(GetDefaultUserConfigurationDirectory(), AppSettingsFileName);
    }

    /// <summary>
    /// Initializes user-owned configuration files from packaged defaults.
    /// </summary>
    /// <param name="configDirectory">Directory that should own the copied files.</param>
    /// <param name="overwrite">Whether existing files should be replaced.</param>
    /// <param name="appSettingsFileName">The appsettings file name to create.</param>
    /// <returns>The initialized configuration paths.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when bundled defaults cannot be found.</exception>
    internal static WickdConfigurationPaths InitializeUserConfiguration(
        string configDirectory,
        bool overwrite = false,
        string appSettingsFileName = AppSettingsFileName)
    {
        try
        {
            var paths = GetConfigurationPaths(configDirectory, appSettingsFileName);
            Directory.CreateDirectory(paths.ConfigDirectory);

            CopyDefaultFile(AppSettingsDefaultsFileName, paths.AppSettingsPath, overwrite);
            CopyDefaultFile(MarketsDefaultsFileName, paths.MarketsPath, overwrite);

            return paths;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WickdConfigurationException(
                $"Could not initialize Wickd configuration in '{configDirectory}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Resolves configuration paths for a directory and appsettings file name.
    /// </summary>
    /// <param name="configDirectory">Directory that owns the configuration files.</param>
    /// <param name="appSettingsFileName">Appsettings file name.</param>
    /// <returns>The resolved configuration paths.</returns>
    internal static WickdConfigurationPaths GetConfigurationPaths(
        string configDirectory,
        string appSettingsFileName)
    {
        var resolvedDirectory = Path.GetFullPath(configDirectory);
        var appSettingsPath = Path.Combine(resolvedDirectory, appSettingsFileName);
        var marketsPath = Path.Combine(resolvedDirectory, MarketsFileName);

        return new WickdConfigurationPaths(resolvedDirectory, appSettingsPath, marketsPath);
    }

    /// <summary>
    /// Loads appsettings.json and applies an optional appsettings.Local.json override from the same directory.
    /// </summary>
    /// <param name="appSettingsPath">Path to the committed appsettings.json file.</param>
    /// <returns>The merged configuration document.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when either settings document is not a JSON object.</exception>
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
    /// <exception cref="WickdConfigurationException">Thrown when the settings root is not a JSON object.</exception>
    private static JsonObject LoadAppSettingsObject(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))
            ?? throw new WickdConfigurationException($"Configuration JSON is empty: {path}");
        if (node is JsonObject jsonObject)
        {
            return jsonObject;
        }

        throw new WickdConfigurationException($"Configuration root must be an object: {path}");
    }

    /// <summary>
    /// Builds the optional local override path for a settings file.
    /// </summary>
    /// <param name="appSettingsPath">Path to the committed appsettings.json file.</param>
    /// <returns>The expected local override path.</returns>
    private static string BuildLocalAppSettingsPath(string appSettingsPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(appSettingsPath))
            ?? throw new WickdConfigurationException($"Cannot resolve directory for {appSettingsPath}.");

        return Path.Combine(directory, "appsettings.Local.json");
    }

    /// <summary>
    /// Copies a bundled default configuration file to a user-owned path.
    /// </summary>
    /// <param name="defaultsFileName">Bundled defaults file name.</param>
    /// <param name="destinationPath">Destination path.</param>
    /// <param name="overwrite">Whether existing files should be replaced.</param>
    private static void CopyDefaultFile(string defaultsFileName, string destinationPath, bool overwrite)
    {
        if (File.Exists(destinationPath) && !overwrite)
        {
            return;
        }

        var sourcePath = FindDefaultTemplatePath(defaultsFileName);
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    /// <summary>
    /// Finds a bundled default template for normal execution, test execution, or repository execution.
    /// </summary>
    /// <param name="fileName">Default template file name.</param>
    /// <returns>The resolved template path.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when the template cannot be found.</exception>
    private static string FindDefaultTemplatePath(string fileName)
    {
        var baseDirectoryCandidate = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(baseDirectoryCandidate))
        {
            return baseDirectoryCandidate;
        }

        var currentDirectoryCandidate = Path.Combine(Environment.CurrentDirectory, "src", "Wickd.Cli", fileName);
        if (File.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        throw new WickdConfigurationException(
            $"Default configuration template was not found. Checked '{baseDirectoryCandidate}' and '{currentDirectoryCandidate}'.");
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
    /// <exception cref="WickdConfigurationException">Thrown when the markets file is missing or invalid.</exception>
    private static IReadOnlyDictionary<string, MarketDefinition> LoadMarkets(string marketsFilePath)
    {
        if (!File.Exists(marketsFilePath))
        {
            throw new WickdConfigurationException($"Markets file was not found: {marketsFilePath}");
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
                throw new WickdConfigurationException($"Duplicate market ID '{marketId}' in markets.json.");
            }

            markets.Add(marketId, new MarketDefinition(marketId, exchangeId, exchangeSymbol));
        }

        if (markets.Count == 0)
        {
            throw new WickdConfigurationException("At least one market must be configured in markets.json.");
        }

        return markets;
    }

    /// <summary>
    /// Parses and validates structure-engine configuration.
    /// </summary>
    /// <param name="structure">The Wickd.Structure object.</param>
    /// <returns>Validated structure settings.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when a required value is missing or invalid.</exception>
    private static StructureSettings ParseStructureSettings(JsonElement structure)
    {
        var minimumSwingSeparationCandles = GetRequiredInt32(
            structure,
            "MinimumSwingSeparationCandles",
            "Wickd.Structure.MinimumSwingSeparationCandles");
        var equalLevelToleranceBasisPoints = GetRequiredDecimal(
            structure,
            "EqualLevelToleranceBasisPoints",
            "Wickd.Structure.EqualLevelToleranceBasisPoints");
        var orderBlockSearchBackCandles = GetRequiredInt32(
            structure,
            "OrderBlockSearchBackCandles",
            "Wickd.Structure.OrderBlockSearchBackCandles");
        var expansionLookbackCandles = GetRequiredInt32(
            structure,
            "ExpansionLookbackCandles",
            "Wickd.Structure.ExpansionLookbackCandles");
        var expansionBodyToAverageRange = GetRequiredDecimal(
            structure,
            "ExpansionBodyToAverageRange",
            "Wickd.Structure.ExpansionBodyToAverageRange");
        var expansionFvgWindowCandles = GetRequiredInt32(
            structure,
            "ExpansionFvgWindowCandles",
            "Wickd.Structure.ExpansionFvgWindowCandles");

        EnsureAtLeast(minimumSwingSeparationCandles, 1, "Wickd.Structure.MinimumSwingSeparationCandles");
        EnsureAtLeast(equalLevelToleranceBasisPoints, 0m, "Wickd.Structure.EqualLevelToleranceBasisPoints");
        EnsureAtLeast(orderBlockSearchBackCandles, 1, "Wickd.Structure.OrderBlockSearchBackCandles");
        EnsureAtLeast(expansionLookbackCandles, 1, "Wickd.Structure.ExpansionLookbackCandles");
        EnsureGreaterThanZero(expansionBodyToAverageRange, "Wickd.Structure.ExpansionBodyToAverageRange");
        EnsureAtLeast(expansionFvgWindowCandles, 1, "Wickd.Structure.ExpansionFvgWindowCandles");

        return new StructureSettings(
            minimumSwingSeparationCandles,
            equalLevelToleranceBasisPoints,
            orderBlockSearchBackCandles,
            expansionLookbackCandles,
            expansionBodyToAverageRange,
            expansionFvgWindowCandles);
    }

    /// <summary>
    /// Gets the default user-profile configuration directory for the current operating system.
    /// </summary>
    /// <returns>The configuration directory path.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when no user profile directory can be resolved.</exception>
    private static string GetDefaultUserConfigurationDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                return Path.Combine(appData, "Wickd");
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                return Path.Combine(home, "Library", "Application Support", "Wickd");
            }
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            return Path.Combine(xdgConfigHome, "wickd");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, ".config", "wickd");
        }

        throw new WickdConfigurationException("Could not resolve a user configuration directory.");
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
            ?? throw new WickdConfigurationException($"Cannot resolve directory for {anchorPath}.");

        return Path.Combine(anchorDirectory, candidatePath);
    }

    /// <summary>
    /// Parses a configured timeframe and reports invalid values as configuration failures.
    /// </summary>
    /// <param name="value">The configured timeframe value.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The parsed timeframe.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when the timeframe is not supported.</exception>
    private static Timeframe ParseConfiguredTimeframe(string value, string path)
    {
        try
        {
            return Timeframe.Parse(value);
        }
        catch (ArgumentException ex)
        {
            throw new WickdConfigurationException($"Configuration value '{path}' is invalid: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Reads a required JSON object property.
    /// </summary>
    /// <param name="parent">The parent JSON element.</param>
    /// <param name="propertyName">The property name to read.</param>
    /// <param name="path">The configuration path used in error messages.</param>
    /// <returns>The required object value.</returns>
    /// <exception cref="WickdConfigurationException">Thrown when the property is missing or is not an object.</exception>
    private static JsonElement GetRequiredObject(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new WickdConfigurationException($"Configuration value '{path}' must be an object.");
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
    /// <exception cref="WickdConfigurationException">Thrown when the property is missing or is not an array.</exception>
    private static JsonElement GetRequiredArray(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new WickdConfigurationException($"Configuration value '{path}' must be an array.");
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
    /// <exception cref="WickdConfigurationException">Thrown when the property is missing, not a string, or empty.</exception>
    private static string GetRequiredString(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.String)
        {
            throw new WickdConfigurationException($"Configuration value '{path}' must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new WickdConfigurationException($"Configuration value '{path}' is required.");
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
    /// <exception cref="WickdConfigurationException">Thrown when the property is missing or is not an integer.</exception>
    private static int GetRequiredInt32(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            throw new WickdConfigurationException($"Configuration value '{path}' must be an integer.");
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
    /// <exception cref="WickdConfigurationException">Thrown when the property is missing or is not numeric.</exception>
    private static decimal GetRequiredDecimal(JsonElement parent, string propertyName, string path)
    {
        var property = GetRequiredProperty(parent, propertyName, path);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var value))
        {
            throw new WickdConfigurationException($"Configuration value '{path}' must be a number.");
        }

        return value;
    }

    /// <summary>
    /// Validates that an integer configuration value is at least the supplied minimum.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minimum">Inclusive minimum value.</param>
    /// <param name="path">Configuration path used in error messages.</param>
    /// <exception cref="WickdConfigurationException">Thrown when the value is too small.</exception>
    private static void EnsureAtLeast(int value, int minimum, string path)
    {
        if (value < minimum)
        {
            throw new WickdConfigurationException(
                $"Configuration value '{path}' must be at least {minimum}.");
        }
    }

    /// <summary>
    /// Validates that a decimal configuration value is at least the supplied minimum.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minimum">Inclusive minimum value.</param>
    /// <param name="path">Configuration path used in error messages.</param>
    /// <exception cref="WickdConfigurationException">Thrown when the value is too small.</exception>
    private static void EnsureAtLeast(decimal value, decimal minimum, string path)
    {
        if (value < minimum)
        {
            throw new WickdConfigurationException(
                $"Configuration value '{path}' must be at least {minimum}.");
        }
    }

    /// <summary>
    /// Validates that a decimal configuration value is positive.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="path">Configuration path used in error messages.</param>
    /// <exception cref="WickdConfigurationException">Thrown when the value is zero or negative.</exception>
    private static void EnsureGreaterThanZero(decimal value, string path)
    {
        if (value <= 0m)
        {
            throw new WickdConfigurationException(
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
    /// <exception cref="WickdConfigurationException">Thrown when the property is missing.</exception>
    private static JsonElement GetRequiredProperty(JsonElement parent, string propertyName, string path)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
        {
            throw new WickdConfigurationException($"Configuration value '{path}' is required.");
        }

        return property;
    }
}
