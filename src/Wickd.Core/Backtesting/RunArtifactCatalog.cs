#nullable enable

using Wickd.Data;

namespace Wickd.Backtesting;

/// <summary>
/// Lists and deletes local backtest run output directories.
/// </summary>
internal static class RunArtifactCatalog
{
    /// <summary>
    /// Lists local run output directories under the configured runs root.
    /// </summary>
    /// <param name="runsRoot">Configured run-output root.</param>
    /// <returns>Run artifacts in deterministic order.</returns>
    /// <exception cref="WickdDataException">Thrown when the runs root cannot be inspected.</exception>
    internal static IReadOnlyList<RunArtifact> List(string runsRoot)
    {
        if (string.IsNullOrWhiteSpace(runsRoot))
        {
            throw new WickdDataException("Runs root is required.");
        }

        var rootPath = Path.GetFullPath(runsRoot);
        if (!Directory.Exists(rootPath))
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateDirectories(rootPath)
                .Select(CreateRunArtifact)
                .OrderBy(run => run.RunId, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WickdDataException(
                $"Could not list runs under {rootPath}. {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Deletes a local run output directory under the configured runs root.
    /// </summary>
    /// <param name="runsRoot">Configured run-output root.</param>
    /// <param name="runId">Run ID to delete.</param>
    /// <returns>The deletion result.</returns>
    /// <exception cref="WickdDataException">Thrown when the run ID is invalid or deletion fails.</exception>
    internal static RunDeletionResult Delete(string runsRoot, string runId)
    {
        if (string.IsNullOrWhiteSpace(runsRoot))
        {
            throw new WickdDataException("Runs root is required.");
        }

        BacktestPipeline.ValidateRunId(runId);

        var rootPath = Path.GetFullPath(runsRoot);
        var targetPath = Path.GetFullPath(Path.Combine(rootPath, runId));
        EnsurePathIsUnderRoot(rootPath, targetPath, "run output directory");

        if (!Directory.Exists(targetPath))
        {
            return new RunDeletionResult(runId, targetPath, Deleted: false);
        }

        try
        {
            Directory.Delete(targetPath, recursive: true);
            return new RunDeletionResult(runId, targetPath, Deleted: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WickdDataException(
                $"Could not delete run '{runId}' at {targetPath}. {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Ensures a resolved target path stays below the configured root.
    /// </summary>
    /// <param name="rootPath">Resolved root path.</param>
    /// <param name="targetPath">Resolved target path.</param>
    /// <param name="description">Artifact description used in diagnostics.</param>
    /// <exception cref="WickdDataException">Thrown when the target escapes the root path.</exception>
    private static void EnsurePathIsUnderRoot(
        string rootPath,
        string targetPath,
        string description)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(rootPath);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        if (string.Equals(normalizedRoot, targetPath, GetPathComparison())
            || !targetPath.StartsWith(rootWithSeparator, GetPathComparison()))
        {
            throw new WickdDataException(
                $"Refusing to delete {description} outside the configured root: {targetPath}");
        }
    }

    /// <summary>
    /// Creates a run artifact summary from one directory.
    /// </summary>
    /// <param name="directoryPath">Run directory path.</param>
    /// <returns>The run artifact summary.</returns>
    private static RunArtifact CreateRunArtifact(string directoryPath)
    {
        var directory = new DirectoryInfo(directoryPath);
        var files = directory
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .ToArray();

        return new RunArtifact(
            directory.Name,
            directory.FullName,
            files.Length,
            files.Sum(file => file.Length),
            directory.LastWriteTimeUtc);
    }

    /// <summary>
    /// Gets the appropriate path comparison for the current operating system.
    /// </summary>
    /// <returns>The path string comparison.</returns>
    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
