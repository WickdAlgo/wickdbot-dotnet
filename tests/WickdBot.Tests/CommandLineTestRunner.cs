using System.CommandLine;

namespace WickdBot.Tests;

/// <summary>
/// Invokes WickdBot commands while safely capturing process-wide console streams.
/// </summary>
internal static class CommandLineTestRunner
{
    /// <summary>
    /// Invokes the production command-line entry point.
    /// </summary>
    /// <param name="args">Command-line arguments to invoke.</param>
    /// <returns>Captured command result.</returns>
    internal static async Task<ConsoleCommandResult> InvokeProgramAsync(string[] args)
    {
        return await CaptureAsync(() => Program.Main(args));
    }

    /// <summary>
    /// Invokes a custom root command.
    /// </summary>
    /// <param name="command">Root command to invoke.</param>
    /// <param name="args">Command-line arguments to invoke.</param>
    /// <returns>Captured command result.</returns>
    internal static async Task<ConsoleCommandResult> InvokeCommandAsync(RootCommand command, string[] args)
    {
        return await CaptureAsync(() => Program.InvokeWithTimingAsync(command, args));
    }

    /// <summary>
    /// Captures console output while running an async command delegate.
    /// </summary>
    /// <param name="invoke">Command delegate to run.</param>
    /// <returns>Captured command result.</returns>
    private static async Task<ConsoleCommandResult> CaptureAsync(Func<Task<int>> invoke)
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

            var exitCode = await invoke();

            return new ConsoleCommandResult(
                exitCode,
                output.ToString(),
                error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
            ConsoleRedirectionLock.SyncRoot.Release();
        }
    }
}
