#nullable enable

namespace WickdBot.Engines;

/// <summary>
/// Represents an expected structure-engine validation or processing failure.
/// </summary>
internal sealed class StructureException : Exception
{
    /// <summary>
    /// Initializes a structure exception with a user-facing message.
    /// </summary>
    /// <param name="message">Failure message.</param>
    internal StructureException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a structure exception with a user-facing message and inner failure.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <param name="innerException">Underlying failure.</param>
    internal StructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
