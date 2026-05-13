namespace WickdBot.Tests;

/// <summary>
/// Captures command-line invocation output for assertions.
/// </summary>
/// <param name="ExitCode">Command exit code.</param>
/// <param name="Output">Captured standard output text.</param>
/// <param name="Error">Captured standard error text.</param>
internal sealed record ConsoleCommandResult(int ExitCode, string Output, string Error);
