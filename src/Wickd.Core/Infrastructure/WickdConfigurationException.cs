namespace Wickd.Infrastructure;

/// <summary>
/// Represents a configuration problem that should be shown at the command-line boundary.
/// </summary>
internal sealed class WickdConfigurationException : Exception
{
    /// <summary>
    /// Initializes a configuration exception with a user-facing message.
    /// </summary>
    /// <param name="message">The configuration error message.</param>
    internal WickdConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a configuration exception with a user-facing message and underlying exception.
    /// </summary>
    /// <param name="message">The configuration error message.</param>
    /// <param name="innerException">The lower-level exception that caused the failure.</param>
    internal WickdConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
