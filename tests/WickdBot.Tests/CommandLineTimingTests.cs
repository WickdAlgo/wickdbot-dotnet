using WickdBot.Data;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Verifies elapsed-time feedback for command-line invocations.
/// </summary>
public class CommandLineTimingTests
{
    /// <summary>
    /// Confirms successful commands write elapsed timing to standard error.
    /// </summary>
    [Fact]
    public async Task SuccessfulCommandWritesTimingToError()
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
        Assert.DoesNotContain("Finished in", result.Output);
        Assert.Matches(CreateTimingPattern(0), result.Error);
    }

    /// <summary>
    /// Confirms parser-level failures still write elapsed timing to standard error.
    /// </summary>
    [Fact]
    public async Task ParserFailureWritesTimingToError()
    {
        using var directory = new TemporaryDirectory();
        var settings = TestSettingsFactory.CreateSettings(directory.DirectoryPath);
        var command = Program.BuildRootCommand(
            new HistoricalDataSource([], CreateCompletedTimeProvider()),
            () => settings);

        var result = await CommandLineTestRunner.InvokeCommandAsync(command, ["fetch"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Finished in", result.Error);
        Assert.Matches(CreateTimingPattern(result.ExitCode), result.Error);
    }

    /// <summary>
    /// Confirms unhandled command exceptions still write elapsed timing before bubbling.
    /// </summary>
    [Fact]
    public async Task UnhandledExceptionWritesTimingBeforeBubbling()
    {
        var error = await CaptureErrorFromThrowingCommandAsync(
            () => Task.FromException<int>(new InvalidOperationException("boom")));

        Assert.Matches(CreateUnavailableExitCodeTimingPattern(), error);
    }

    /// <summary>
    /// Confirms elapsed durations are formatted at the expected display precision.
    /// </summary>
    /// <param name="milliseconds">Elapsed milliseconds to format.</param>
    /// <param name="expected">Expected duration text.</param>
    [Theory]
    [InlineData(83, "83 ms")]
    [InlineData(1234, "1.23 s")]
    [InlineData(75200, "1 min 15 s")]
    public void FormatElapsedDurationUsesExpectedPrecision(int milliseconds, string expected)
    {
        var formatted = Program.FormatElapsedDuration(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(expected, formatted);
    }

    /// <summary>
    /// Creates a regex that matches the standard command timing footer.
    /// </summary>
    /// <param name="exitCode">Expected exit code in the footer.</param>
    /// <returns>Regex pattern for the timing footer.</returns>
    private static string CreateTimingPattern(int exitCode)
    {
        return $@"Finished in (?:\d+ ms|\d+\.\d{{2}} s|\d+ min \d{{2}} s) \(exit code {exitCode}\)\.";
    }

    /// <summary>
    /// Creates a regex that matches the command timing footer when no exit code is available.
    /// </summary>
    /// <returns>Regex pattern for an exceptional timing footer.</returns>
    private static string CreateUnavailableExitCodeTimingPattern()
    {
        return @"Finished in (?:\d+ ms|\d+\.\d{2} s|\d+ min \d{2} s) \(exit code unavailable\)\.";
    }

    /// <summary>
    /// Captures standard error while asserting a command invocation throws.
    /// </summary>
    /// <param name="command">Root command to invoke.</param>
    /// <param name="args">Command-line arguments to invoke.</param>
    /// <returns>Captured standard error text.</returns>
    private static async Task<string> CaptureErrorFromThrowingCommandAsync(Func<Task<int>> invoke)
    {
        var originalOutput = Console.Out;
        var originalError = Console.Error;

        await ConsoleRedirectionLock.SyncRoot.WaitAsync();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            Console.SetOut(output);
            Console.SetError(error);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => Program.InvokeWithTimingAsync(invoke));
            Assert.Equal("boom", exception.Message);

            return error.ToString();
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
            ConsoleRedirectionLock.SyncRoot.Release();
        }
    }

    /// <summary>
    /// Creates an exchange candle for CLI timing tests.
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
    /// Creates a clock that makes test request ranges fully completed.
    /// </summary>
    /// <returns>The completed-range time provider.</returns>
    private static TimeProvider CreateCompletedTimeProvider()
    {
        return new FixedTimeProvider(new DateTimeOffset(2026, 5, 7, 0, 0, 0, TimeSpan.Zero));
    }
}
