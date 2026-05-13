using WickdBot.Data;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Verifies local dataset alias catalog persistence and resolution behavior.
/// </summary>
public class DatasetAliasCatalogTests
{
    /// <summary>
    /// Confirms aliases can be created, loaded, and resolved into run requests.
    /// </summary>
    [Fact]
    public async Task SaveLoadAndResolveAlias()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = CreateCatalog(directory);

        await catalog.SaveAsync(
            "may6-session",
            CreateHistoricalDataResult(request),
            force: false);

        var aliases = await catalog.LoadAsync();
        var alias = Assert.Single(aliases);
        var resolved = await catalog.ResolveRunRequestAsync("may6-session", settings);

        Assert.Equal("may6-session", alias.Alias);
        Assert.Equal(request.Market.MarketId, resolved.Market.MarketId);
        Assert.Equal(request.Timeframe, resolved.Timeframe);
        Assert.Equal(request.DateRange, resolved.DateRange);
        Assert.Equal(request.CandleCachePath, resolved.CandleCachePath);
    }

    /// <summary>
    /// Confirms missing aliases fail with a clear message.
    /// </summary>
    [Fact]
    public async Task ResolveRejectsMissingAlias()
    {
        using var directory = new TemporaryDirectory();
        var catalog = CreateCatalog(directory);

        var exception = await Assert.ThrowsAsync<DatasetAliasException>(
            () => catalog.ResolveAsync("missing"));

        Assert.Contains("Dataset alias 'missing' was not found", exception.Message);
    }

    /// <summary>
    /// Confirms duplicate aliases fail unless overwrite is explicit.
    /// </summary>
    [Fact]
    public async Task SaveRejectsDuplicateAliasWithoutForce()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = CreateCatalog(directory);
        await catalog.SaveAsync(
            "may6-session",
            CreateHistoricalDataResult(request),
            force: false);

        var exception = await Assert.ThrowsAsync<DatasetAliasException>(
            () => catalog.SaveAsync(
                "may6-session",
                CreateHistoricalDataResult(request),
                force: false));

        Assert.Contains("already exists", exception.Message);
    }

    /// <summary>
    /// Confirms force updates an existing alias while preserving its creation timestamp.
    /// </summary>
    [Fact]
    public async Task SaveForceOverwritesAlias()
    {
        using var directory = new TemporaryDirectory();
        var catalog = CreateCatalog(directory);
        var firstRequest = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        var secondRequest = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 1, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 1, 5, 0, TimeSpan.Zero));

        var first = await catalog.SaveAsync(
            "may6-session",
            CreateHistoricalDataResult(firstRequest),
            force: false);
        var second = await catalog.SaveAsync(
            "may6-session",
            CreateHistoricalDataResult(secondRequest),
            force: true);
        var resolved = await catalog.ResolveAsync("may6-session");

        Assert.Equal(first.CreatedAtUtc, second.CreatedAtUtc);
        Assert.Equal(secondRequest.DateRange.FromUtc, resolved.FromUtc);
        Assert.Equal(secondRequest.CandleCachePath, resolved.CandleCachePath);
    }

    /// <summary>
    /// Confirms unsupported alias characters are rejected.
    /// </summary>
    [Fact]
    public async Task SaveRejectsInvalidAlias()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = CreateCatalog(directory);

        var exception = await Assert.ThrowsAsync<DatasetAliasException>(
            () => catalog.SaveAsync(
                "bad alias",
                CreateHistoricalDataResult(request),
                force: false));

        Assert.Contains("may contain only", exception.Message);
    }

    /// <summary>
    /// Confirms expected alias catalog write failures are reported as alias exceptions.
    /// </summary>
    [Fact]
    public async Task SaveWrapsCatalogWriteFailures()
    {
        using var directory = new TemporaryDirectory();
        var catalogPath = Path.Combine(directory.DirectoryPath, "datasets.json");
        Directory.CreateDirectory(catalogPath);
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = new DatasetAliasCatalog(
            catalogPath,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<DatasetAliasException>(
            () => catalog.SaveAsync(
                "may6-session",
                CreateHistoricalDataResult(request),
                force: false));

        Assert.Contains("Could not write dataset alias catalog", exception.Message);
        Assert.Contains(catalogPath, exception.Message);
    }

    /// <summary>
    /// Creates an alias catalog inside a temporary directory.
    /// </summary>
    /// <param name="directory">Temporary directory owner.</param>
    /// <returns>The alias catalog.</returns>
    private static DatasetAliasCatalog CreateCatalog(TemporaryDirectory directory)
    {
        return new DatasetAliasCatalog(
            Path.Combine(directory.DirectoryPath, "datasets.json"),
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Creates a historical data result for alias catalog tests.
    /// </summary>
    /// <param name="request">Run request referenced by the result.</param>
    /// <returns>The historical data result.</returns>
    private static HistoricalDataResult CreateHistoricalDataResult(RunRequest request)
    {
        return new HistoricalDataResult(
            request,
            [],
            [],
            request.CandleCachePath,
            CacheHit: false);
    }
}
