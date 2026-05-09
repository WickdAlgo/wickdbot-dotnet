namespace WickdBot.Tests;

/// <summary>
/// Verifies that the public command-line interface accepts the planned MVP command shapes.
/// </summary>
public class CommandLineWiringTests
{
    /// <summary>
    /// Example invocations for commands whose interfaces are locked before implementation.
    /// </summary>
    public static TheoryData<string, string[]> NotImplementedCommandExamples => new()
    {
        {
            "fetch",
            [
                "fetch",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-07T07:00:00Z"
            ]
        },
        {
            "backtest",
            [
                "backtest",
                "--market",
                "BTC_USDT_PERP",
                "--timeframe",
                "5m",
                "--from",
                "2026-05-06T00:00:00Z",
                "--to",
                "2026-05-07T07:00:00Z"
            ]
        },
        {
            "analyze",
            [
                "analyze",
                "--run-id",
                "run-000001"
            ]
        }
    };

    /// <summary>
    /// Confirms each supported command parses valid arguments and reaches its placeholder handler.
    /// </summary>
    /// <param name="commandName">The command expected to be invoked.</param>
    /// <param name="args">A complete command-line argument list for the command.</param>
    [Theory]
    [MemberData(nameof(NotImplementedCommandExamples))]
    public void SupportedCommandsParseAndReturnNotImplemented(string commandName, string[] args)
    {
        var originalError = Console.Error;
        using var error = new StringWriter();

        try
        {
            Console.SetError(error);

            var exitCode = Program.Main(args);

            Assert.Equal(Program.NotImplementedExitCode, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains($"Command '{commandName}' is not implemented yet.", error.ToString());
    }
}
