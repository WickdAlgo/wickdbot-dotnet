#nullable enable

namespace Wickd.Backtesting;

/// <summary>
/// Summarizes a local run output deletion attempt.
/// </summary>
/// <param name="RunId">Requested run ID.</param>
/// <param name="DirectoryPath">Resolved run output directory path.</param>
/// <param name="Deleted">Whether a run directory was deleted.</param>
internal sealed record RunDeletionResult(
    string RunId,
    string DirectoryPath,
    bool Deleted);
