#nullable enable

namespace WickdBot.Data;

/// <summary>
/// Represents an expected historical data loading, fetching, caching, or replay failure.
/// </summary>
internal sealed class WickdBotDataException : Exception
{
    /// <summary>
    /// Initializes a data exception with a user-facing message.
    /// </summary>
    /// <param name="message">Failure message.</param>
    internal WickdBotDataException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a data exception with a user-facing message and root cause.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <param name="innerException">Root cause exception.</param>
    internal WickdBotDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
