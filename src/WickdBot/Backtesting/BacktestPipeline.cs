#nullable enable

using WickdBot.Data;
using WickdBot.Engines;
using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot.Backtesting;

/// <summary>
/// Orchestrates the deterministic Phase 3 backtest path from replayed candles to structure journals.
/// </summary>
internal sealed class BacktestPipeline
{
    private readonly HistoricalDataSource historicalDataSource;
    private readonly StructureEngine structureEngine;

    /// <summary>
    /// Initializes the backtest pipeline.
    /// </summary>
    /// <param name="historicalDataSource">Historical data source used to replay cached candles.</param>
    /// <param name="structureEngine">Structure engine used to process replayed candles.</param>
    internal BacktestPipeline(
        HistoricalDataSource historicalDataSource,
        StructureEngine? structureEngine = null)
    {
        this.historicalDataSource = historicalDataSource;
        this.structureEngine = structureEngine ?? new StructureEngine();
    }

    /// <summary>
    /// Replays cached candles, runs structure detection, and writes structures.jsonl.
    /// </summary>
    /// <param name="request">Resolved backtest run request.</param>
    /// <param name="runId">Backtest run ID.</param>
    /// <param name="settings">Validated WickdBot settings.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The Phase 3 backtest pipeline result.</returns>
    /// <exception cref="WickdBotDataException">Thrown when replay or structure journal writing fails.</exception>
    /// <exception cref="StructureException">Thrown when replayed candles violate structure-engine assumptions.</exception>
    internal async Task<BacktestPipelineResult> RunAsync(
        RunRequest request,
        string runId,
        WickdBotSettings settings,
        CancellationToken cancellationToken = default)
    {
        ValidateRunId(runId);

        var replayResult = await historicalDataSource.ReplayAsync(request, runId, cancellationToken);
        var structureResult = structureEngine.Process(replayResult.Candles, settings.Structure);
        var structuresPath = BuildStructuresPath(settings.RunsRoot, runId);

        try
        {
            await StructureJsonLines.WriteAsync(
                structuresPath,
                structureResult.Events,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WickdBotDataException(
                $"Could not write structure journal: {structuresPath}. {ex.Message}",
                ex);
        }

        return new BacktestPipelineResult(
            runId,
            replayResult,
            structureResult,
            structuresPath);
    }

    /// <summary>
    /// Builds the run-specific structures.jsonl path.
    /// </summary>
    /// <param name="runsRoot">Configured run-output root.</param>
    /// <param name="runId">Backtest run ID.</param>
    /// <returns>The structure journal path.</returns>
    internal static string BuildStructuresPath(string runsRoot, string runId)
    {
        return Path.Combine(runsRoot, runId, "structures.jsonl");
    }

    /// <summary>
    /// Ensures a run ID can be used as one local path segment.
    /// </summary>
    /// <param name="runId">Run ID to validate.</param>
    /// <exception cref="WickdBotDataException">Thrown when the run ID is empty, path-like, or contains unsupported characters.</exception>
    private static void ValidateRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new WickdBotDataException("Backtest run ID is required.");
        }

        var hasLetterOrDigit = false;
        foreach (var character in runId)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.')
            {
                throw new WickdBotDataException(CreateInvalidRunIdMessage());
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                hasLetterOrDigit = true;
            }
        }

        if (!hasLetterOrDigit || runId is "." or "..")
        {
            throw new WickdBotDataException(CreateInvalidRunIdMessage());
        }
    }

    /// <summary>
    /// Creates the shared validation message for unsafe run IDs.
    /// </summary>
    /// <returns>The validation message.</returns>
    private static string CreateInvalidRunIdMessage()
    {
        return "Backtest run ID must include at least one letter or digit and can only contain letters, numbers, '.', '_', and '-'.";
    }
}
