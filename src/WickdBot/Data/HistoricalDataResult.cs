#nullable enable

using WickdBot.Models;

namespace WickdBot.Data;

/// <summary>
/// Summarizes historical candle cache loading or fetching for a run request.
/// </summary>
/// <param name="Request">Resolved run request that produced the result.</param>
/// <param name="Candles">Normalized historical candles.</param>
/// <param name="Gaps">Detected non-fatal timeframe gaps.</param>
/// <param name="CachePath">JSONL candle cache path used by the request.</param>
/// <param name="CacheHit">Whether candles were loaded from an existing cache file.</param>
internal sealed record HistoricalDataResult(
    RunRequest Request,
    IReadOnlyList<CandleEvent> Candles,
    IReadOnlyList<CandleGap> Gaps,
    string CachePath,
    bool CacheHit)
{
    /// <summary>
    /// Gets the number of normalized candles in the result.
    /// </summary>
    internal int CandleCount => Candles.Count;
}
