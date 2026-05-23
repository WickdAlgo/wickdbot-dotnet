#nullable enable

namespace Wickd.Engines;

/// <summary>
/// Summarizes deterministic structure processing for one backtest candle stream.
/// </summary>
/// <param name="RunId">Backtest run ID.</param>
/// <param name="Events">Structure events emitted in deterministic processing order.</param>
/// <param name="Snapshot">Final structure state after processing.</param>
public sealed record StructureProcessingResult(
    string RunId,
    IReadOnlyList<StructureEvent> Events,
    StructureSnapshot Snapshot)
{
    /// <summary>
    /// Gets the number of emitted structure events.
    /// </summary>
    public int EventCount => Events.Count;
}
