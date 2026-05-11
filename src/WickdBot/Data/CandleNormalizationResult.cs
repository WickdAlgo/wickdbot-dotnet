using WickdBot.Models;

namespace WickdBot.Data;

/// <summary>
/// Contains normalized candles plus non-fatal gap metadata.
/// </summary>
/// <param name="Candles">Ordered, deduplicated candles.</param>
/// <param name="Gaps">Detected timeframe gaps.</param>
internal sealed record CandleNormalizationResult(
    IReadOnlyList<CandleEvent> Candles,
    IReadOnlyList<CandleGap> Gaps);
