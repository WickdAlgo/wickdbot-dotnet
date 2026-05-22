using Wickd.Data;
using Wickd.Models;

namespace Wickd.Tests;

/// <summary>
/// Verifies that the public command-line interface accepts the planned MVP command shapes.
/// </summary>
public class CommandLineWiringTests
{
    /// <summary>
    /// Confirms fetch parses valid arguments and creates a deterministic candle cache.
    /// </summary>
    [Fact]
    public async Task FetchParsesAndCreatesCandleCache()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var fakeClient = new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => Task.FromResult<IReadOnlyList<ExchangeCandle>>(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]));
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([fakeClient], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "fetch",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Fetched 1 candles", result.Output);
        Assert.Contains("cache miss", result.Output);
        Assert.Equal(1, fakeClient.FetchCount);
    }

    /// <summary>
    /// Confirms fetch can save a friendly dataset alias after cache creation.
    /// </summary>
    [Fact]
    public async Task FetchAliasWritesDatasetCatalog()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var fakeClient = new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => Task.FromResult<IReadOnlyList<ExchangeCandle>>(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]));
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([fakeClient], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "fetch",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z",
                "--alias",
                "may6-session"
            ]);
        var catalog = DatasetAliasCatalog.CreateDefault(settings);
        var alias = await catalog.ResolveAsync("may6-session");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Saved dataset alias 'may6-session'", result.Output);
        Assert.Equal("BTC_USDT_PERP", alias.MarketId);
        Assert.Equal("5m", alias.Timeframe);
    }

    /// <summary>
    /// Confirms fetch rejects duplicate aliases unless overwrite is explicit.
    /// </summary>
    [Fact]
    public async Task FetchAliasRejectsDuplicateWithoutForce()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var fakeClient = new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => Task.FromResult<IReadOnlyList<ExchangeCandle>>(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]));
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([fakeClient], CreateCompletedTimeProvider()),
            () => settings);

        await InvokeFetchAliasAsync(command, "may6-session");
        var result = await InvokeFetchAliasAsync(command, "may6-session");

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("already exists", result.Error);
    }

    /// <summary>
    /// Confirms fetch can overwrite duplicate aliases when --force is supplied.
    /// </summary>
    [Fact]
    public async Task FetchAliasForceOverwritesDuplicate()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var fakeClient = new FakeMarketDataClient(
            "binance",
            (_, _, _, _) => Task.FromResult<IReadOnlyList<ExchangeCandle>>(
            [
                CreateExchangeCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]));
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([fakeClient], CreateCompletedTimeProvider()),
            () => settings);

        await InvokeFetchAliasAsync(command, "may6-session");
        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "fetch",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z",
                "--alias",
                "may6-session",
                "--force"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Saved dataset alias 'may6-session'", result.Output);
    }

    /// <summary>
    /// Confirms backtest parses valid arguments and replays cached candle input.
    /// </summary>
    [Fact]
    public async Task BacktestParsesAndReplaysCachedCandles()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z",
                "--run-id",
                "phase-3-smoke"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Replayed 1 candles for run 'phase-3-smoke'", result.Output);
        Assert.Contains("Wrote 2 structure events", result.Output);
        Assert.Contains("Setup and trade execution are not implemented yet", result.Output);
        Assert.True(File.Exists(Path.Combine(settings.RunsRoot, "phase-3-smoke", "structures.jsonl")));
        Assert.False(File.Exists(Path.Combine(settings.RunsRoot, "phase-3-smoke", "setups.jsonl")));
        Assert.False(File.Exists(Path.Combine(settings.RunsRoot, "phase-3-smoke", "trades.jsonl")));
        Assert.False(File.Exists(Path.Combine(settings.RunsRoot, "phase-3-smoke", "outcomes.jsonl")));
    }

    /// <summary>
    /// Confirms backtest accepts run IDs that use the supported separator characters with letters or digits.
    /// </summary>
    [Fact]
    public async Task BacktestAcceptsRunIdWithDotAndUnderscore()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z",
                "--run-id",
                "phase_3.smoke"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Replayed 1 candles for run 'phase_3.smoke'", result.Output);
        Assert.True(File.Exists(Path.Combine(settings.RunsRoot, "phase_3.smoke", "structures.jsonl")));
    }

    /// <summary>
    /// Confirms backtest still accepts the generated UTC run ID when --run-id is omitted.
    /// </summary>
    [Fact]
    public async Task BacktestAcceptsGeneratedUtcRunId()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Replayed 1 candles for run '", result.Output);
    }

    /// <summary>
    /// Confirms backtest can resolve candle input from a saved dataset alias.
    /// </summary>
    [Fact]
    public async Task BacktestDatasetAliasReplaysCachedCandles()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]);
        var catalog = DatasetAliasCatalog.CreateDefault(settings);
        await catalog.SaveAsync(
            "may6-session",
            new HistoricalDataResult(request, [], [], request.CandleCachePath, CacheHit: true),
            force: false);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "backtest",
                "--dataset",
                "may6-session",
                "--run-id",
                "phase-2-test"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Replayed 1 candles for run 'phase-2-test'", result.Output);
        Assert.True(File.Exists(Path.Combine(settings.RunsRoot, "phase-2-test", "structures.jsonl")));
    }

    /// <summary>
    /// Confirms backtest rejects dataset aliases mixed with explicit range options.
    /// </summary>
    [Fact]
    public async Task BacktestDatasetAliasRejectsMixedExplicitOptions()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "backtest",
                "--dataset",
                "may6-session",
                "--market",
                "BTC_USDT_PERP",
                "--run-id",
                "phase-2-test"
            ]);

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Use either --dataset or explicit", result.Error);
    }

    /// <summary>
    /// Confirms backtest reports missing dataset aliases clearly.
    /// </summary>
    [Fact]
    public async Task BacktestDatasetAliasRejectsMissingAlias()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "backtest",
                "--dataset",
                "missing",
                "--run-id",
                "phase-2-test"
            ]);

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Dataset alias 'missing' was not found", result.Error);
    }

    /// <summary>
    /// Confirms dataset management can list saved aliases.
    /// </summary>
    [Fact]
    public async Task ManageDatasetsListShowsSavedAliases()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(directory.DirectoryPath);
        var catalog = DatasetAliasCatalog.CreateDefault(settings);
        await catalog.SaveAsync(
            "may6-session",
            new HistoricalDataResult(request, [], [], request.CandleCachePath, CacheHit: true),
            force: false);
        var command = Program.BuildRootCommand(loadSettings: () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "manage",
                "datasets",
                "list"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Dataset aliases:", result.Output);
        Assert.Contains("may6-session | BTC_USDT_PERP | binance | 5m", result.Output);
    }

    /// <summary>
    /// Confirms dataset management can delete an alias and its unshared cache file.
    /// </summary>
    [Fact]
    public async Task ManageDatasetsDeleteRemovesAliasAndCacheFile()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var request = TestSettingsFactory.CreateRunRequest(
            directory.DirectoryPath,
            fromUtc: new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
            toUtc: new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero));
        await CandleJsonLines.WriteAsync(
            request.CandleCachePath,
            [
                CreateHistoricalCandle(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero))
            ]);
        var catalog = DatasetAliasCatalog.CreateDefault(settings);
        await catalog.SaveAsync(
            "may6-session",
            new HistoricalDataResult(request, [], [], request.CandleCachePath, CacheHit: true),
            force: false);
        var command = Program.BuildRootCommand(loadSettings: () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "manage",
                "datasets",
                "delete",
                "--alias",
                "may6-session",
                "--delete-cache"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Deleted dataset alias 'may6-session'", result.Output);
        Assert.Contains("Deleted dataset cache file", result.Output);
        Assert.False(File.Exists(request.CandleCachePath));
        Assert.Empty(await catalog.LoadAsync());
    }

    /// <summary>
    /// Confirms run management can list local run output directories.
    /// </summary>
    [Fact]
    public async Task ManageRunsListShowsRunDirectories()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var runDirectory = Path.Combine(settings.RunsRoot, "phase-3-smoke");
        Directory.CreateDirectory(runDirectory);
        File.WriteAllText(Path.Combine(runDirectory, "structures.jsonl"), "{}");
        var command = Program.BuildRootCommand(loadSettings: () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "manage",
                "runs",
                "list"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Runs:", result.Output);
        Assert.Contains("phase-3-smoke | files 1", result.Output);
    }

    /// <summary>
    /// Confirms run deletion requires explicit force confirmation.
    /// </summary>
    [Fact]
    public async Task ManageRunsDeleteRequiresForce()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var runDirectory = Path.Combine(settings.RunsRoot, "phase-3-smoke");
        Directory.CreateDirectory(runDirectory);
        var command = Program.BuildRootCommand(loadSettings: () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "manage",
                "runs",
                "delete",
                "--run-id",
                "phase-3-smoke"
            ]);

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("requires --force", result.Error);
        Assert.True(Directory.Exists(runDirectory));
    }

    /// <summary>
    /// Confirms run management can delete local run output directories.
    /// </summary>
    [Fact]
    public async Task ManageRunsDeleteRemovesRunDirectory()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var runDirectory = Path.Combine(settings.RunsRoot, "phase-3-smoke");
        Directory.CreateDirectory(runDirectory);
        File.WriteAllText(Path.Combine(runDirectory, "structures.jsonl"), "{}");
        var command = Program.BuildRootCommand(loadSettings: () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "manage",
                "runs",
                "delete",
                "--run-id",
                "phase-3-smoke",
                "--force"
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Deleted run 'phase-3-smoke'", result.Output);
        Assert.False(Directory.Exists(runDirectory));
    }

    /// <summary>
    /// Confirms analyze remains the Phase 1 placeholder command.
    /// </summary>
    [Fact]
    public async Task AnalyzeParsesAndReturnsNotImplemented()
    {
        var result = await CommandLineTestRunner.InvokeProgramAsync(
            [
                "analyze",
                "--run-id",
                "run-000001"
            ]);

        Assert.Equal(Program.NotImplementedExitCode, result.ExitCode);
        Assert.Contains("Command 'analyze' is not implemented yet.", result.Error);
    }

    /// <summary>
    /// Creates an exchange candle for CLI tests.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <returns>The exchange candle.</returns>
    private static ExchangeCandle CreateExchangeCandle(DateTimeOffset openTimeUtc)
    {
        return new ExchangeCandle(
            openTimeUtc,
            open: 100m,
            high: 103m,
            low: 99m,
            close: 101m,
            volume: 12.5m);
    }

    /// <summary>
    /// Creates a cached historical candle for CLI tests.
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

    /// <summary>
    /// Invokes fetch with a common test alias and deterministic range.
    /// </summary>
    /// <param name="command">Root command to invoke.</param>
    /// <param name="alias">Dataset alias to save.</param>
    /// <returns>The command result.</returns>
    private static async Task<ConsoleCommandResult> InvokeFetchAliasAsync(
        System.CommandLine.RootCommand command,
        string alias)
    {
        return await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "fetch",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:05:00Z",
                "--alias",
                alias
            ]);
    }

    /// <summary>
    /// Creates a clock that makes test request ranges fully completed.
    /// </summary>
    /// <returns>The completed-range time provider.</returns>
    private static TimeProvider CreateCompletedTimeProvider()
    {
        return new FixedTimeProvider(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));
    }
}
