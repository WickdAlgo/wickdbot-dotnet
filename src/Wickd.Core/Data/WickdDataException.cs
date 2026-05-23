#nullable enable

namespace Wickd.Data;

/// <summary>
/// Represents an expected historical data loading, fetching, caching, or replay failure.
/// </summary>
public sealed class WickdDataException : Exception
{
    /// <summary>
    /// Initializes a data exception with a user-facing message.
    /// </summary>
    /// <param name="message">Failure message.</param>
    public WickdDataException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a data exception with a user-facing message and root cause.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <param name="innerException">Root cause exception.</param>
    public WickdDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
