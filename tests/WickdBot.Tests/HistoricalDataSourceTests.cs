using WickdBot.Data;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Verifies deterministic historical data fetching, caching, and replay behavior.
/// </summary>
public class HistoricalDataSourceTests
{
    /// <summary>
    /// Confirms cache misses fetch exchange candles, normalize them, and write only normalized historical candles.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchWritesNormalizedCacheOnMiss()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var firstCandle = CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero));
        var fakeClient = new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => Task.FromResult<IReadOnlyList<ExchangeCandle>>(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero)),
                firstCandle,
                firstCandle
            ]));
        var source = CreateSource(fakeClient);

        var result = await source.LoadOrFetchAsync(request);

        Assert.False(result.CacheHit);
        Assert.Equal(2, result.CandleCount);
        Assert.Equal(1, fakeClient.FetchCount);
        Assert.True(File.Exists(request.CandleCachePath));

        var persistedCandles = await CandleJsonLines.ReadAsync(request.CandleCachePath);
        Assert.Equal(
            [
                new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero)
            ],
            persistedCandles.Select(candle => candle.OpenTimeUtc));
        Assert.All(persistedCandles, candle => Assert.Equal(CandleSource.Historical, candle.Source));
        Assert.All(persistedCandles, candle => Assert.Null(candle.RunId));
    }

    /// <summary>
    /// Confirms existing caches are read without calling the market data client.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchUsesCacheWithoutFetching()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero)),
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero))
            ]);
        var fakeClient = new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => throw new InvalidOperationException("Cache hit should not fetch."));
        var source = CreateSource(fakeClient);

        var result = await source.LoadOrFetchAsync(request);

        Assert.True(result.CacheHit);
        Assert.Equal(2, result.CandleCount);
        Assert.Equal(0, fakeClient.FetchCount);
    }

    /// <summary>
    /// Confirms unsupported configured exchanges fail before any fetch attempt.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchRejectsUnsupportedExchange()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            TestSettingsFactory.HyperliquidMarket);
        var source = CreateSource();

        var exception = await Assert.ThrowsAsync<WickdBotDataException>(
            () => source.LoadOrFetchAsync(request));

        Assert.Contains("Exchange 'hyperliquid' is not supported", exception.Message);
    }

    /// <summary>
    /// Confirms requests that include incomplete candles fail before fetching.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchRejectsIncompleteCandleRange()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        var source = new HistoricalDataSource(
            [CreateFakeClient([])],
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 6, 0, 3, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<WickdBotDataException>(
            () => source.LoadOrFetchAsync(request));

        Assert.Contains("includes incomplete candles", exception.Message);
    }

    /// <summary>
    /// Confirms conflicting duplicate candle data fails before cache write.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchRejectsConflictingDuplicateCandles()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var openTime = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero);
        var source = CreateSource(
            CreateFakeClient(
            [
                CreateExchangeCandle(openTime, close: 101m),
                CreateExchangeCandle(openTime, close: 102m)
            ]));

        var exception = await Assert.ThrowsAsync<WickdBotDataException>(
            () => source.LoadOrFetchAsync(request));

        Assert.Contains("Conflicting candles share open time", exception.Message);
        Assert.False(File.Exists(request.CandleCachePath));
    }

    /// <summary>
    /// Confirms expected cache write failures are reported as data exceptions.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchWrapsCacheWriteFailures()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        Directory.CreateDirectory(request.CandleCachePath);
        var source = CreateSource(
            CreateFakeClient(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]));

        var exception = await Assert.ThrowsAsync<WickdBotDataException>(
            () => source.LoadOrFetchAsync(request));

        Assert.Contains("Could not write candle cache", exception.Message);
        Assert.Contains(request.CandleCachePath, exception.Message);
    }

    /// <summary>
    /// Confirms timeframe gaps are reported as metadata and do not fail the cache.
    /// </summary>
    [Fact]
    public async Task LoadOrFetchReportsGaps()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var source = CreateSource(
            CreateFakeClient(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero)),
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 10, 0, TimeSpan.Zero))
            ]));

        var result = await source.LoadOrFetchAsync(request);

        var gap = Assert.Single(result.Gaps);
        Assert.Equal(new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero), gap.ExpectedOpenTimeUtc);
        Assert.Equal(new DateTimeOffset(2026, 5, 6, 0, 10, 0, TimeSpan.Zero), gap.ActualOpenTimeUtc);
        Assert.Equal(1, gap.MissingCandles);
    }

    /// <summary>
    /// Confirms replay converts cached historical candles into backtest candles with the supplied run ID.
    /// </summary>
    [Fact]
    public async Task ReplayConvertsHistoricalCandlesToBacktestRun()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]);
        var source = CreateSource();

        var result = await source.ReplayAsync(request, "phase-2-test");

        var candle = Assert.Single(result.Candles);
        Assert.Equal(CandleSource.Backtest, candle.Source);
        Assert.Equal("phase-2-test", candle.RunId);
        Assert.Equal(request.CandleCachePath, result.CachePath);
    }

    /// <summary>
    /// Confirms replay requires a pre-existing deterministic candle cache.
    /// </summary>
    [Fact]
    public async Task ReplayRequiresExistingCache()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var source = CreateSource();

        var exception = await Assert.ThrowsAsync<WickdBotDataException>(
            () => source.ReplayAsync(request, "phase-2-test"));

        Assert.Contains("Run fetch before backtest", exception.Message);
    }

    /// <summary>
    /// Confirms cached candles must match the requested market identity.
    /// </summary>
    [Fact]
    public async Task ReplayRejectsMismatchedCacheIdentity()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                new CandleEvent(
                    new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
                    "OTHER_MARKET",
                    "binance",
                    "5m",
                    open: 100m,
                    high: 103m,
                    low: 99m,
                    close: 101m,
                    volume: 12.5m,
                    CandleSource.Historical)
            ]);
        var source = CreateSource();

        var exception = await Assert.ThrowsAsync<WickdBotDataException>(
            () => source.ReplayAsync(request, "phase-2-test"));

        Assert.Contains("does not match requested market", exception.Message);
    }

    /// <summary>
    /// Creates a historical data source with a completed-candle clock.
    /// </summary>
    /// <param name="clients">Market data clients to register.</param>
    /// <returns>The historical data source.</returns>
    private static HistoricalDataSource CreateSource(params IMarketDataClient[] clients)
    {
        return new HistoricalDataSource(
            clients,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Creates a fake Binance client that returns predefined candles.
    /// </summary>
    /// <param name="candles">Exchange candles to return.</param>
    /// <returns>The fake market data client.</returns>
    private static FakeMarketDataClient CreateFakeClient(IReadOnlyList<ExchangeCandle> candles)
    {
        return new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => Task.FromResult(candles));
    }

    /// <summary>
    /// Creates an exchange candle for historical data source tests.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <param name="close">Close price.</param>
    /// <returns>The exchange candle.</returns>
    private static ExchangeCandle CreateExchangeCandle(DateTimeOffset openTimeUtc, decimal close = 101m)
    {
        return new ExchangeCandle(
            openTimeUtc,
            open: 100m,
            high: 103m,
            low: 99m,
            close: close,
            volume: 12.5m);
    }

    /// <summary>
    /// Creates a historical candle for cache tests.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <returns>The historical candle.</returns>
    private static CandleEvent CreateHistoricalCandle(DateTimeOffset openTimeUtc)
    {
        return new CandleEvent(
            openTimeUtc,
            "BTC_USDT_PERP",
            "binance",
            "5m",
            open: 100m,
            high: 103m,
            low: 99m,
            close: 101m,
            volume: 12.5m,
            CandleSource.Historical);
    }
}
