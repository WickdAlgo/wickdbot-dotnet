#nullable enable

namespace Wickd.Engines;

/// <summary>
/// Summarizes the final structure-engine state after processing a candle stream.
/// </summary>
/// <param name="RunId">Backtest run ID.</param>
/// <param name="MarketId">Canonical Wickd market ID.</param>
/// <param name="ExchangeId">Exchange identifier that produced the candles.</param>
/// <param name="Timeframe">Canonical candle timeframe.</param>
/// <param name="ActiveOrderBlockIds">Order-block IDs that remain active at the end of processing.</param>
/// <param name="ActiveFvgIds">Fair-value gap IDs that remain at least partially open at the end of processing.</param>
public sealed record StructureSnapshot(
    string RunId,
    string MarketId,
    string ExchangeId,
    string Timeframe,
    IReadOnlyList<string> ActiveOrderBlockIds,
    IReadOnlyList<string> ActiveFvgIds);
