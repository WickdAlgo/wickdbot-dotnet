#nullable enable

using Wickd.Models;

namespace Wickd.Data;

/// <summary>
/// Summarizes replayed backtest candles loaded from a historical candle cache.
/// </summary>
/// <param name="RunId">Backtest run ID assigned to replayed candles.</param>
/// <param name="CachePath">JSONL candle cache path used for replay.</param>
/// <param name="Candles">Replayed backtest-source candles.</param>
/// <param name="Gaps">Detected non-fatal timeframe gaps from the source cache.</param>
public sealed record CandleReplayResult(
    string RunId,
    string CachePath,
    IReadOnlyList<CandleEvent> Candles,
    IReadOnlyList<CandleGap> Gaps)
{
    /// <summary>
    /// Gets the number of replayed candles.
    /// </summary>
    public int CandleCount => Candles.Count;
}
