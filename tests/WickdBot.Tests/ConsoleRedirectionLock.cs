namespace WickdBot.Tests;

/// <summary>
/// Coordinates tests that temporarily redirect process-wide console streams.
/// </summary>
internal static class ConsoleRedirectionLock
{
    /// <summary>
    /// Gets the shared lock used around console stream redirection.
    /// </summary>
    internal static object SyncRoot { get; } = new();
}
