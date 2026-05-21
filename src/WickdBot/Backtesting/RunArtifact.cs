#nullable enable

namespace WickdBot.Backtesting;

/// <summary>
/// Describes one local run output directory.
/// </summary>
/// <param name="RunId">Run ID represented by the directory name.</param>
/// <param name="DirectoryPath">Resolved run output directory path.</param>
/// <param name="FileCount">Number of files under the run directory.</param>
/// <param name="TotalBytes">Total size of files under the run directory.</param>
/// <param name="LastWriteTimeUtc">Most recent write timestamp for the run directory.</param>
internal sealed record RunArtifact(
    string RunId,
    string DirectoryPath,
    int FileCount,
    long TotalBytes,
    DateTimeOffset LastWriteTimeUtc);
