namespace WickdBot.Tests;

/// <summary>
/// Provides a deterministic UTC timestamp for completed-candle validation tests.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    /// <summary>
    /// UTC timestamp returned by this provider.
    /// </summary>
    private readonly DateTimeOffset utcNow;

    /// <summary>
    /// Initializes a deterministic time provider.
    /// </summary>
    /// <param name="utcNow">UTC timestamp to return.</param>
    internal FixedTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow;
    }

    /// <summary>
    /// Gets the configured deterministic UTC timestamp.
    /// </summary>
    /// <returns>The configured UTC timestamp.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}
