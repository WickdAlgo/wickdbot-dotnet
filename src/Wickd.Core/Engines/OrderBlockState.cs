#nullable enable

namespace Wickd.Engines;

/// <summary>
/// Represents the lifecycle state of a detected order block.
/// </summary>
public enum OrderBlockState
{
    /// <summary>
    /// The order block has been discovered and has not been touched by later price action.
    /// </summary>
    Active,

    /// <summary>
    /// A later candle has wick-touched the order block zone.
    /// </summary>
    Mitigated,

    /// <summary>
    /// The order block has been consumed by the first mitigation touch.
    /// </summary>
    Consumed,

    /// <summary>
    /// Reserved for a later rule that proves an order block is structurally invalid.
    /// </summary>
    Invalidated,

    /// <summary>
    /// Reserved for a later setup rule that rejects an order block candidate.
    /// </summary>
    Rejected
}
