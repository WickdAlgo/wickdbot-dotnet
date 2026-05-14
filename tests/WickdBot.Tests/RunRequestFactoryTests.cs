using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Verifies command request resolution against validated settings.
/// </summary>
public class RunRequestFactoryTests
{
    /// <summary>
    /// Confirms request resolution derives the expected candle cache path.
    /// </summary>
    [Fact]
    public void CreateBuildsExpectedCandleCachePath()
    {
        var request = RunRequestFactory.Create(
            CreateSettings(),
            "BTC_USDT_PERP",
            "5m",
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 7, 0, 0, TimeSpan.Zero));

        var expectedPath = Path.Combine(
            "data/cache",
            "binance",
            "BTC_USDT_PERP",
            "5m",
            "2026-05-06T00-00-00.0000000Z_2026-05-07T07-00-00.0000000Z",
            "candles.jsonl");

        Assert.Equal(expectedPath, request.CandleCachePath);
    }

    /// <summary>
    /// Confirms different intraday windows do not collide in the candle cache path.
    /// </summary>
    [Fact]
    public void CreateIncludesFullUtcRangeInCandleCachePath()
    {
        var morningRequest = RunRequestFactory.Create(
            CreateSettings(),
            "BTC_USDT_PERP",
            "5m",
            new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 6, 1, 0, 0, TimeSpan.Zero));
        var afternoonRequest = RunRequestFactory.Create(
            CreateSettings(),
            "BTC_USDT_PERP",
            "5m",
            new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 6, 13, 0, 0, TimeSpan.Zero));

        Assert.NotEqual(morningRequest.CandleCachePath, afternoonRequest.CandleCachePath);
        Assert.Contains("2026-05-06T00-00-00.0000000Z_2026-05-06T01-00-00.0000000Z", morningRequest.CandleCachePath);
        Assert.Contains("2026-05-06T12-00-00.0000000Z_2026-05-06T13-00-00.0000000Z", afternoonRequest.CandleCachePath);
    }

    /// <summary>
    /// Confirms identical UTC windows with different timeframes do not collide in the candle cache path.
    /// </summary>
    /// <param name="firstTimeframe">First requested timeframe.</param>
    /// <param name="secondTimeframe">Second requested timeframe.</param>
    [Theory]
    [InlineData("5m", "15m")]
    [InlineData("5m", "4h")]
    [InlineData("15m", "4h")]
    public void CreateIncludesTimeframeInCandleCachePath(string firstTimeframe, string secondTimeframe)
    {
        var fromUtc = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(2026, 5, 7, 7, 0, 0, TimeSpan.Zero);
        var firstRequest = RunRequestFactory.Create(
            CreateSettings(),
            "BTC_USDT_PERP",
            firstTimeframe,
            fromUtc,
            toUtc);
        var secondRequest = RunRequestFactory.Create(
            CreateSettings(),
            "BTC_USDT_PERP",
            secondTimeframe,
            fromUtc,
            toUtc);

        Assert.NotEqual(firstRequest.CandleCachePath, secondRequest.CandleCachePath);
        Assert.Contains(Path.DirectorySeparatorChar + firstTimeframe + Path.DirectorySeparatorChar, firstRequest.CandleCachePath);
        Assert.Contains(Path.DirectorySeparatorChar + secondTimeframe + Path.DirectorySeparatorChar, secondRequest.CandleCachePath);
    }

    /// <summary>
    /// Confirms date ranges must move forward in UTC time.
    /// </summary>
    [Fact]
    public void CreateRejectsInvalidDateRange()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => RunRequestFactory.Create(
                CreateSettings(),
                "BTC_USDT_PERP",
                "5m",
                new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero)));

        Assert.Contains("end time must be later", exception.Message);
    }

    /// <summary>
    /// Confirms non-UTC date endpoints are rejected.
    /// </summary>
    [Fact]
    public void CreateRejectsNonUtcDateRange()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => RunRequestFactory.Create(
                CreateSettings(),
                "BTC_USDT_PERP",
                "5m",
                new DateTimeOffset(2026, 5, 6, 3, 0, 0, TimeSpan.FromHours(3)),
                new DateTimeOffset(2026, 5, 7, 7, 0, 0, TimeSpan.Zero)));

        Assert.Contains("start time must be expressed in UTC", exception.Message);
    }

    /// <summary>
    /// Creates validated settings with one configured BTC/USDT perpetual market.
    /// </summary>
    /// <returns>The test settings.</returns>
    private static WickdBotSettings CreateSettings()
    {
        var market = new MarketDefinition("BTC_USDT_PERP", "binance", "BTC/USDT:USDT");
        return new WickdBotSettings(
            "BTC_USDT_PERP",
            Timeframe.Parse("5m"),
            "markets.json",
            "data/cache",
            "runs",
            TestSettingsFactory.CreateStructureSettings(),
            new Dictionary<string, MarketDefinition>(StringComparer.Ordinal)
            {
                [market.MarketId] = market
            });
    }
}
