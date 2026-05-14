using WickdBot.Infrastructure;

namespace WickdBot.Tests;

/// <summary>
/// Verifies appsettings.json and markets.json loading rules.
/// </summary>
public class ConfigurationLoaderTests
{
    /// <summary>
    /// Confirms valid configuration resolves a configured market.
    /// </summary>
    [Fact]
    public void LoadResolvesConfiguredMarkets()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(directory, ValidMarketsJson);

        var settings = WickdBotConfigurationLoader.Load(appSettingsPath);
        var market = settings.ResolveMarket("BTC_USDT_PERP");

        Assert.Equal("BTC_USDT_PERP", settings.DefaultMarketId);
        Assert.Equal("5m", settings.DefaultTimeframe.Value);
        Assert.Equal(2, settings.Structure.MinimumSwingSeparationCandles);
        Assert.Equal(1.5m, settings.Structure.ExpansionBodyToAverageRange);
        Assert.Equal("binance", market.ExchangeId);
        Assert.Equal("BTC/USDT:USDT", market.ExchangeSymbol);
    }

    /// <summary>
    /// Confirms local appsettings overrides replace committed defaults without rewriting appsettings.json.
    /// </summary>
    [Fact]
    public void LoadAppliesLocalStructureOverride()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(directory, ValidMarketsJson);
        directory.WriteFile(
            "appsettings.Local.json",
            """
            {
              "WickdBot": {
                "Structure": {
                  "MinimumSwingSeparationCandles": 4,
                  "ExpansionBodyToAverageRange": 2.25
                }
              }
            }
            """);

        var settings = WickdBotConfigurationLoader.Load(appSettingsPath);

        Assert.Equal(4, settings.Structure.MinimumSwingSeparationCandles);
        Assert.Equal(5m, settings.Structure.EqualLevelToleranceBasisPoints);
        Assert.Equal(2.25m, settings.Structure.ExpansionBodyToAverageRange);
    }

    /// <summary>
    /// Confirms invalid structure settings fail as configuration errors.
    /// </summary>
    [Fact]
    public void LoadRejectsInvalidStructureSettings()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(
            directory,
            ValidMarketsJson,
            structureJson:
            """
                "Structure": {
                  "MinimumSwingSeparationCandles": 0,
                  "EqualLevelToleranceBasisPoints": 5,
                  "OrderBlockSearchBackCandles": 3,
                  "ExpansionLookbackCandles": 20,
                  "ExpansionBodyToAverageRange": 1.5,
                  "ExpansionFvgWindowCandles": 2
                }
            """);

        var exception = Assert.Throws<WickdBotConfigurationException>(() => WickdBotConfigurationLoader.Load(appSettingsPath));

        Assert.Contains("WickdBot.Structure.MinimumSwingSeparationCandles", exception.Message);
        Assert.Contains("at least 1", exception.Message);
    }

    /// <summary>
    /// Confirms duplicate canonical market IDs fail fast.
    /// </summary>
    [Fact]
    public void LoadRejectsDuplicateMarketIds()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(
            directory,
            """
            {
              "Markets": [
                {
                  "MarketId": "BTC_USDT_PERP",
                  "ExchangeId": "binance",
                  "ExchangeSymbol": "BTC/USDT:USDT"
                },
                {
                  "MarketId": "BTC_USDT_PERP",
                  "ExchangeId": "hyperliquid",
                  "ExchangeSymbol": "BTC/USDC:USDC"
                }
              ]
            }
            """);

        var exception = Assert.Throws<WickdBotConfigurationException>(() => WickdBotConfigurationLoader.Load(appSettingsPath));

        Assert.Contains("Duplicate market ID 'BTC_USDT_PERP'", exception.Message);
    }

    /// <summary>
    /// Confirms required market identity fields are validated.
    /// </summary>
    /// <param name="marketsJson">The markets.json contents.</param>
    /// <param name="expectedMessage">The expected error message fragment.</param>
    [Theory]
    [MemberData(nameof(MissingMarketFieldExamples))]
    public void LoadRejectsMissingMarketIdentityFields(string marketsJson, string expectedMessage)
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(directory, marketsJson);

        var exception = Assert.Throws<WickdBotConfigurationException>(() => WickdBotConfigurationLoader.Load(appSettingsPath));

        Assert.Contains(expectedMessage, exception.Message);
    }

    /// <summary>
    /// Confirms an unsupported default timeframe is rejected.
    /// </summary>
    [Fact]
    public void LoadRejectsInvalidDefaultTimeframe()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(directory, ValidMarketsJson, timeframe: "2m");

        var exception = Assert.Throws<WickdBotConfigurationException>(() => WickdBotConfigurationLoader.Load(appSettingsPath));

        Assert.Contains("WickdBot.Defaults.Timeframe", exception.Message);
        Assert.Contains("Unsupported timeframe '2m'", exception.Message);
    }

    /// <summary>
    /// Confirms unknown requested markets fail before command execution.
    /// </summary>
    [Fact]
    public void ResolveMarketRejectsUnknownMarket()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = WriteConfiguration(directory, ValidMarketsJson);
        var settings = WickdBotConfigurationLoader.Load(appSettingsPath);

        var exception = Assert.Throws<WickdBotConfigurationException>(() => settings.ResolveMarket("UNKNOWN_MARKET"));

        Assert.Contains("Unknown market 'UNKNOWN_MARKET'", exception.Message);
    }

    /// <summary>
    /// Examples for required market field validation.
    /// </summary>
    public static TheoryData<string, string> MissingMarketFieldExamples => new()
    {
        {
            """
            {
              "Markets": [
                {
                  "ExchangeId": "binance",
                  "ExchangeSymbol": "BTC/USDT:USDT"
                }
              ]
            }
            """,
            "Markets[].MarketId"
        },
        {
            """
            {
              "Markets": [
                {
                  "MarketId": "BTC_USDT_PERP",
                  "ExchangeSymbol": "BTC/USDT:USDT"
                }
              ]
            }
            """,
            "Markets[].ExchangeId"
        },
        {
            """
            {
              "Markets": [
                {
                  "MarketId": "BTC_USDT_PERP",
                  "ExchangeId": "binance"
                }
              ]
            }
            """,
            "Markets[].ExchangeSymbol"
        }
    };

    /// <summary>
    /// Valid single-market markets.json fixture used by configuration loader tests.
    /// </summary>
    private const string ValidMarketsJson = """
        {
          "Markets": [
            {
              "MarketId": "BTC_USDT_PERP",
              "ExchangeId": "binance",
              "ExchangeSymbol": "BTC/USDT:USDT"
            }
          ]
        }
        """;

    /// <summary>
    /// Writes paired appsettings.json and markets.json fixtures into a temporary directory.
    /// </summary>
    /// <param name="directory">The temporary directory that owns the files.</param>
    /// <param name="marketsJson">The markets.json contents.</param>
    /// <param name="defaultMarket">The default market ID to write to appsettings.json.</param>
    /// <param name="timeframe">The default timeframe to write to appsettings.json.</param>
    /// <returns>The full path to the written appsettings.json file.</returns>
    private static string WriteConfiguration(
        TemporaryDirectory directory,
        string marketsJson,
        string defaultMarket = "BTC_USDT_PERP",
        string timeframe = "5m",
        string? structureJson = null)
    {
        directory.WriteFile("markets.json", marketsJson);
        structureJson ??=
            """
                "Structure": {
                  "MinimumSwingSeparationCandles": 2,
                  "EqualLevelToleranceBasisPoints": 5,
                  "OrderBlockSearchBackCandles": 3,
                  "ExpansionLookbackCandles": 20,
                  "ExpansionBodyToAverageRange": 1.5,
                  "ExpansionFvgWindowCandles": 2
                }
            """;

        return directory.WriteFile(
            "appsettings.json",
            $$"""
            {
              "WickdBot": {
                "Defaults": {
                  "MarketId": "{{defaultMarket}}",
                  "Timeframe": "{{timeframe}}",
                  "MarketsFilePath": "markets.json"
                },
                "Storage": {
                  "CacheRoot": "data/cache",
                  "RunsRoot": "runs"
                },
            {{structureJson}}
              }
            }
            """);
    }
}
