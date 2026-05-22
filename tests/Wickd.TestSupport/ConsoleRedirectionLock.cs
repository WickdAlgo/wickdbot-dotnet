namespace Wickd.Tests;

/// <summary>
/// Coordinates tests that temporarily redirect process-wide console streams.
/// </summary>
internal static class ConsoleRedirectionLock
{
    /// <summary>
    /// Gets the shared semaphore used around console stream redirection.
    /// </summary>
    internal static SemaphoreSlim SyncRoot { get; } = new(1, 1);
}
