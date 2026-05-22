#nullable enable

namespace Wickd.Engines;

/// <summary>
/// Represents an expected structure-engine validation or processing failure.
/// </summary>
public sealed class StructureException : Exception
{
    /// <summary>
    /// Initializes a structure exception with a user-facing message.
    /// </summary>
    /// <param name="message">Failure message.</param>
    public StructureException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a structure exception with a user-facing message and inner failure.
    /// </summary>
    /// <param name="message">Failure message.</param>
    /// <param name="innerException">Underlying failure.</param>
    public StructureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
