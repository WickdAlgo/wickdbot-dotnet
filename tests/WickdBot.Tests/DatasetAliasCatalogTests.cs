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
    /// Confirms aliases can be deleted while preserving the rest of the catalog.
    /// </summary>
    [Fact]
    public async Task DeleteRemovesAliasAndPreservesRemainingAliases()
    {
        using var directory = new TemporaryDirectory();
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = CreateCatalog(directory);
        await catalog.SaveAsync(
            "first",
            CreateHistoricalDataResult(request),
            force: false);
        await catalog.SaveAsync(
            "second",
            CreateHistoricalDataResult(request),
            force: false);

        var result = await catalog.DeleteAsync("first");
        var aliases = await catalog.LoadAsync();
        var remaining = Assert.Single(aliases);

        Assert.Equal("first", result.DeletedAlias.Alias);
        Assert.Single(result.RemainingAliases);
        Assert.Equal("second", remaining.Alias);
    }

    /// <summary>
    /// Confirms deleting a missing alias fails with a clear message.
    /// </summary>
    [Fact]
    public async Task DeleteRejectsMissingAlias()
    {
        using var directory = new TemporaryDirectory();
        var catalog = CreateCatalog(directory);

        var exception = await Assert.ThrowsAsync<DatasetAliasException>(
            () => catalog.DeleteAsync("missing"));

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
    /// Confirms concurrent alias saves preserve every update in the catalog.
    /// </summary>
    [Fact]
    public async Task SaveConcurrentAliasesPreservesAllUpdates()
    {
        using var directory = new TemporaryDirectory();
        var catalogPath = Path.Combine(directory.DirectoryPath, "datasets.json");
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var saveTasks = Enumerable.Range(0, 12)
            .Select(index => new DatasetAliasCatalog(
                    catalogPath,
                    new FixedTimeProvider(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero)))
                .SaveAsync(
                    $"session-{index:00}",
                    CreateHistoricalDataResult(request),
                    force: false))
            .ToArray();

        await Task.WhenAll(saveTasks);

        var aliases = await new DatasetAliasCatalog(catalogPath).LoadAsync();

        Assert.Equal(12, aliases.Count);
        Assert.Equal(
            Enumerable.Range(0, 12).Select(index => $"session-{index:00}"),
            aliases.Select(alias => alias.Alias));
    }

    /// <summary>
    /// Confirms concurrent duplicate saves still allow only one non-forced alias creation.
    /// </summary>
    [Fact]
    public async Task SaveConcurrentDuplicateAliasAllowsOnlyOneWithoutForce()
    {
        using var directory = new TemporaryDirectory();
        var catalogPath = Path.Combine(directory.DirectoryPath, "datasets.json");
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var result = CreateHistoricalDataResult(request);
        var saveTasks = Enumerable.Range(0, 2)
            .Select(_ => CaptureAliasSaveExceptionAsync(
                new DatasetAliasCatalog(
                    catalogPath,
                    new FixedTimeProvider(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero))),
                "may6-session",
                result))
            .ToArray();

        var exceptions = await Task.WhenAll(saveTasks);
        var aliases = await new DatasetAliasCatalog(catalogPath).LoadAsync();
        var exception = Assert.Single(exceptions.Where(item => item is not null));
        var alias = Assert.Single(aliases);

        Assert.Single(exceptions.Where(item => item is null));
        Assert.Contains("already exists", exception!.Message);
        Assert.Equal("may6-session", alias.Alias);
    }

    /// <summary>
    /// Confirms failed atomic writes leave an existing valid catalog readable.
    /// </summary>
    [Fact]
    public async Task SaveWriteFailurePreservesExistingCatalog()
    {
        using var directory = new TemporaryDirectory();
        var catalogPath = Path.Combine(directory.DirectoryPath, "datasets.json");
        var originalRequest = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = new DatasetAliasCatalog(
            catalogPath,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero)));
        await catalog.SaveAsync(
            "original",
            CreateHistoricalDataResult(originalRequest),
            force: false);
        var blockedTempPath = Path.Combine(directory.DirectoryPath, "blocked-temp");
        Directory.CreateDirectory(blockedTempPath);
        var failingCatalog = new DatasetAliasCatalog(
            catalogPath,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 13, 0, 1, 0, TimeSpan.Zero)),
            _ => blockedTempPath);
        var nextRequest = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 1, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 1, 5, 0, TimeSpan.Zero));

        var exception = await Assert.ThrowsAsync<DatasetAliasException>(
            () => failingCatalog.SaveAsync(
                "next",
                CreateHistoricalDataResult(nextRequest),
                force: false));
        var aliases = await catalog.LoadAsync();
        var alias = Assert.Single(aliases);

        Assert.Contains("Could not write dataset alias catalog", exception.Message);
        Assert.Equal("original", alias.Alias);
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
    /// Captures duplicate alias save failures without failing concurrent save tests early.
    /// </summary>
    /// <param name="catalog">Alias catalog to save into.</param>
    /// <param name="alias">Alias to save.</param>
    /// <param name="result">Historical data result to reference.</param>
    /// <returns>The alias exception, or <see langword="null" /> when the save succeeds.</returns>
    private static async Task<DatasetAliasException?> CaptureAliasSaveExceptionAsync(
        DatasetAliasCatalog catalog,
        string alias,
        HistoricalDataResult result)
    {
        try
        {
            await catalog.SaveAsync(alias, result, force: false);
            return null;
        }
        catch (DatasetAliasException ex)
        {
            return ex;
        }
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
