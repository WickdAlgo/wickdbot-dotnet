#nullable enable

namespace Wickd.Data;

/// <summary>
/// Represents an expected dataset alias validation, lookup, or persistence failure.
/// </summary>
internal sealed class DatasetAliasException : Exception
{
    /// <summary>
    /// Initializes an alias exception with a user-facing message.
    /// </summary>
    /// <param name="message">Failure message.</param>
    internal DatasetAliasException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes an alias exception with a user-facing message and root cause.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <param name="innerException">Root cause exception.</param>
    internal DatasetAliasException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
