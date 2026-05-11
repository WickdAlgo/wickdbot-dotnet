namespace WickdBot.Tests;

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
    public void RunCommandsValidateArgumentsBeforeReturningNotImplemented(string[] args, string expectedMessage)
    {
        var originalError = Console.Error;
        string errorText;
        int exitCode;

        lock (ConsoleRedirectionLock.SyncRoot)
        {
            using var error = new StringWriter();

            try
            {
                Console.SetError(error);

                exitCode = Program.Main(args);
                errorText = error.ToString();
            }
            finally
            {
                Console.SetError(originalError);
            }
        }

        Assert.Equal(Program.ValidationErrorExitCode, exitCode);
        Assert.Contains(expectedMessage, errorText);
        Assert.DoesNotContain("not implemented yet", errorText);
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
        }
    };
}
