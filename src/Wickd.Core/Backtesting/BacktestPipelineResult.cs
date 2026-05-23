#nullable enable

using Wickd.Data;
using Wickd.Engines;

namespace Wickd.Backtesting;

/// <summary>
/// Summarizes the Phase 3 backtest pipeline output.
/// </summary>
/// <param name="RunId">Backtest run ID.</param>
/// <param name="ReplayResult">Replayed candle input summary.</param>
/// <param name="StructureResult">Structure engine output summary.</param>
/// <param name="StructuresPath">Path to the written structures.jsonl journal.</param>
internal sealed record BacktestPipelineResult(
    string RunId,
    CandleReplayResult ReplayResult,
    StructureProcessingResult StructureResult,
    string StructuresPath);
