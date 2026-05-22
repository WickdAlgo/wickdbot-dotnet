#nullable enable

namespace Wickd.Engines;

/// <summary>
/// Represents the lifecycle state of a fair-value gap.
/// </summary>
public enum FvgState
{
    /// <summary>
    /// The fair-value gap is still at least partially open.
    /// </summary>
    Active,

    /// <summary>
    /// Wick penetration has filled the complete fair-value gap zone.
    /// </summary>
    Filled
}
