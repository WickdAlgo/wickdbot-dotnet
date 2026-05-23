using Wickd.Data;

namespace Wickd.Tests;

/// <summary>
/// Verifies command handlers validate configured request arguments before placeholder behavior.
/// </summary>
public class CommandLineValidationTests
{
    /// <summary>
    /// Confirms invalid fetch and backtest requests do not reach the placeholder handler.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="expectedMessage">Expected validation message fragment.</param>
    [Theory]
    [MemberData(nameof(InvalidRunCommandExamples))]
    public async Task RunCommandsValidateArgumentsBeforeReachingDataExecution(string[] args, string expectedMessage)
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(command, args);

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains(expectedMessage, result.Error);
        Assert.DoesNotContain("not implemented yet", result.Error);
    }

    /// <summary>
    /// Confirms unsupported configured exchanges fail clearly at data-source resolution.
    /// </summary>
    [Fact]
    public async Task FetchRejectsUnsupportedExchange()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(
            directory.DirectoryPath,
            TestSettingsFactory.HyperliquidMarket);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "fetch",
                "--market",
                "BTC_USDC_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:15:00Z"
            ]);

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Exchange 'hyperliquid' is not supported", result.Error);
    }

    /// <summary>
    /// Confirms backtest fails clearly when the deterministic candle cache has not been fetched.
    /// </summary>
    [Fact]
    public async Task BacktestRequiresExistingCandleCache()
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
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:15:00Z",
                "--run-id",
                "phase-2-test"
            ]);

        Assert.Equal(Program.ValidationErrorExitCode, result.ExitCode);
        Assert.Contains("Run fetch before backtest", result.Error);
    }

    /// <summary>
    /// Invalid command examples for request validation.
    /// </summary>
    public static TheoryData<string[], string> InvalidRunCommandExamples => new()
    {
        {
            [
                "fetch",
                "--market",
                "UNKNOWN_MARKET",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-07T07:00:00Z"
            ],
            "Unknown market 'UNKNOWN_MARKET'"
        },
        {
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "2m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-07T07:00:00Z"
            ],
            "Unsupported timeframe '2m'"
        },
        {
            [
                "fetch",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-07T07:00:00Z",
                "--to",
                "2026-05-06T00:00:00Z"
            ],
            "end time must be later"
        },
        {
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:15:00Z",
                "--run-id",
                "."
            ],
            "must include at least one letter or digit"
        },
        {
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:15:00Z",
                "--run-id",
                ".."
            ],
            "must include at least one letter or digit"
        },
        {
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-06T00:15:00Z",
                "--run-id",
                "..."
            ],
            "must include at least one letter or digit"
        }
    };

    /// <summary>
    /// Creates a clock that makes test request ranges fully completed.
    /// </summary>
    /// <returns>The completed-range time provider.</returns>
    private static TimeProvider CreateCompletedTimeProvider()
    {
        return new FixedTimeProvider(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));
    }
}
