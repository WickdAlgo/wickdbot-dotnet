#nullable enable

using System.Text.Json;
using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot.Data;

/// <summary>
/// Persists and resolves friendly aliases for deterministic candle cache ranges.
/// </summary>
internal sealed class DatasetAliasCatalog
{
    /// <summary>
    /// Delay between attempts to acquire the catalog lock.
    /// </summary>
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Serializer options shared by dataset alias reads and writes.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Local JSON catalog path.
    /// </summary>
    private readonly string catalogPath;

    /// <summary>
    /// Clock used for alias timestamps.
    /// </summary>
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates temporary catalog paths for atomic writes.
    /// </summary>
    private readonly Func<string, string> createTempCatalogPath;

    /// <summary>
    /// Initializes a dataset alias catalog.
    /// </summary>
    /// <param name="catalogPath">Local JSON catalog path.</param>
    /// <param name="timeProvider">Clock used for alias timestamps.</param>
    /// <param name="createTempCatalogPath">Factory used to create temporary catalog paths for atomic writes.</param>
    internal DatasetAliasCatalog(
        string catalogPath,
        TimeProvider? timeProvider = null,
        Func<string, string>? createTempCatalogPath = null)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            throw new DatasetAliasException("Dataset alias catalog path is required.");
        }

        this.catalogPath = catalogPath;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.createTempCatalogPath = createTempCatalogPath ?? CreateTempCatalogPath;
    }

    /// <summary>
    /// Creates the default dataset alias catalog for loaded settings.
    /// </summary>
    /// <param name="settings">Loaded WickdBot settings.</param>
    /// <returns>The default dataset alias catalog.</returns>
    internal static DatasetAliasCatalog CreateDefault(WickdBotSettings settings)
    {
        return new DatasetAliasCatalog(GetDefaultCatalogPath(settings));
    }

    /// <summary>
    /// Gets the default local dataset alias catalog path for loaded settings.
    /// </summary>
    /// <param name="settings">Loaded WickdBot settings.</param>
    /// <returns>The default catalog path.</returns>
    internal static string GetDefaultCatalogPath(WickdBotSettings settings)
    {
        var cacheRoot = settings.CacheRoot;
        var cacheRootFullPath = Path.GetFullPath(cacheRoot);
        var cacheRootName = Path.GetFileName(
            cacheRootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (string.Equals(cacheRootName, "cache", StringComparison.OrdinalIgnoreCase))
        {
            var dataRoot = Path.GetDirectoryName(cacheRootFullPath)
                ?? throw new DatasetAliasException($"Cannot resolve data root for cache root '{cacheRoot}'.");
            return Path.Combine(dataRoot, "datasets.json");
        }

        return Path.Combine(cacheRoot, "datasets.json");
    }

    /// <summary>
    /// Saves or updates an alias for a historical data result.
    /// </summary>
    /// <param name="alias">Friendly dataset alias.</param>
    /// <param name="result">Historical data result to reference.</param>
    /// <param name="force">Whether an existing alias may be overwritten.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The saved dataset alias.</returns>
    /// <exception cref="DatasetAliasException">Thrown when the alias exists and overwrite is not allowed.</exception>
    internal async Task<DatasetAlias> SaveAsync(
        string alias,
        HistoricalDataResult result,
        bool force,
        CancellationToken cancellationToken = default)
    {
        DatasetAlias.ValidateAlias(alias);

        await using var catalogLock = await OpenCatalogLockAsync(cancellationToken);
        var aliases = await LoadAsync(cancellationToken);
        return await SaveLockedAsync(alias, result, force, aliases, cancellationToken);
    }

    /// <summary>
    /// Deletes an existing alias from the local catalog.
    /// </summary>
    /// <param name="alias">Friendly dataset alias.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The removed alias and aliases left in the catalog.</returns>
    /// <exception cref="DatasetAliasException">Thrown when the alias is missing.</exception>
    internal async Task<DatasetAliasDeletionResult> DeleteAsync(
        string alias,
        CancellationToken cancellationToken = default)
    {
        DatasetAlias.ValidateAlias(alias);

        await using var catalogLock = await OpenCatalogLockAsync(cancellationToken);
        var aliases = await LoadAsync(cancellationToken);
        var existing = aliases.SingleOrDefault(item => item.Alias == alias);
        if (existing is null)
        {
            throw new DatasetAliasException($"Dataset alias '{alias}' was not found.");
        }

        var updatedAliases = aliases
            .Where(item => item.Alias != alias)
            .OrderBy(item => item.Alias, StringComparer.Ordinal)
            .ToArray();

        await SaveAllAsync(updatedAliases, cancellationToken);
        return new DatasetAliasDeletionResult(existing, updatedAliases);
    }

    /// <summary>
    /// Resolves an existing dataset alias into a run request.
    /// </summary>
    /// <param name="alias">Friendly dataset alias.</param>
    /// <param name="settings">Loaded WickdBot settings.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The run request referenced by the alias.</returns>
    /// <exception cref="DatasetAliasException">Thrown when the alias is missing or no longer matches configuration.</exception>
    internal async Task<RunRequest> ResolveRunRequestAsync(
        string alias,
        WickdBotSettings settings,
        CancellationToken cancellationToken = default)
    {
        var dataset = await ResolveAsync(alias, cancellationToken);
        var market = settings.ResolveMarket(dataset.MarketId);
        if (market.ExchangeId != dataset.ExchangeId)
        {
            throw new DatasetAliasException(
                $"Dataset alias '{alias}' exchange '{dataset.ExchangeId}' does not match configured exchange '{market.ExchangeId}'.");
        }

        var timeframe = Timeframe.Parse(dataset.Timeframe);
        return new RunRequest(
            market,
            timeframe,
            new DateRange(dataset.FromUtc, dataset.ToUtc),
            dataset.CandleCachePath);
    }

    /// <summary>
    /// Resolves an existing dataset alias.
    /// </summary>
    /// <param name="alias">Friendly dataset alias.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The resolved dataset alias.</returns>
    /// <exception cref="DatasetAliasException">Thrown when the alias is missing.</exception>
    internal async Task<DatasetAlias> ResolveAsync(
        string alias,
        CancellationToken cancellationToken = default)
    {
        DatasetAlias.ValidateAlias(alias);
        var aliases = await LoadAsync(cancellationToken);
        var dataset = aliases.SingleOrDefault(item => item.Alias == alias);
        if (dataset is null)
        {
            throw new DatasetAliasException($"Dataset alias '{alias}' was not found.");
        }

        return dataset;
    }

    /// <summary>
    /// Loads all aliases from the local catalog.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>Dataset aliases in catalog order.</returns>
    internal async Task<IReadOnlyList<DatasetAlias>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(catalogPath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(catalogPath);
            var document = await JsonSerializer.DeserializeAsync<DatasetAliasDocument>(
                stream,
                SerializerOptions,
                cancellationToken);

            return document?.Datasets ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or DatasetAliasException)
        {
            throw new DatasetAliasException(
                $"Dataset alias catalog is invalid: {catalogPath}. {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Writes the complete alias catalog to disk.
    /// </summary>
    /// <param name="aliases">Aliases to persist.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    private async Task SaveAllAsync(
        IReadOnlyCollection<DatasetAlias> aliases,
        CancellationToken cancellationToken)
    {
        var tempPath = createTempCatalogPath(catalogPath);

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(catalogPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new DatasetAliasDocument(aliases.ToArray()),
                    SerializerOptions,
                    cancellationToken);
                await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(catalogPath))
            {
                File.Replace(tempPath, catalogPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, catalogPath);
            }
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            throw new DatasetAliasException(
                $"Could not write dataset alias catalog: {catalogPath}. {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Saves an alias while the catalog lock is held.
    /// </summary>
    /// <param name="alias">Friendly dataset alias.</param>
    /// <param name="result">Historical data result to reference.</param>
    /// <param name="force">Whether an existing alias may be overwritten.</param>
    /// <param name="aliases">Aliases loaded while the catalog lock is held.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The saved dataset alias.</returns>
    private async Task<DatasetAlias> SaveLockedAsync(
        string alias,
        HistoricalDataResult result,
        bool force,
        IReadOnlyList<DatasetAlias> aliases,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var existing = aliases.SingleOrDefault(item => item.Alias == alias);

        if (existing is not null && !force)
        {
            throw new DatasetAliasException(
                $"Dataset alias '{alias}' already exists. Use --force to overwrite it.");
        }

        var saved = new DatasetAlias(
            alias,
            result.Request.Market.MarketId,
            result.Request.Market.ExchangeId,
            result.Request.Timeframe.Value,
            result.Request.DateRange.FromUtc,
            result.Request.DateRange.ToUtc,
            result.CachePath,
            existing?.CreatedAtUtc ?? nowUtc,
            nowUtc);

        var updatedAliases = aliases
            .Where(item => item.Alias != alias)
            .Append(saved)
            .OrderBy(item => item.Alias, StringComparer.Ordinal)
            .ToArray();

        await SaveAllAsync(updatedAliases, cancellationToken);
        return saved;
    }

    /// <summary>
    /// Opens the exclusive catalog lock file, waiting until another writer releases it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for waiting on the lock.</param>
    /// <returns>The opened catalog lock stream.</returns>
    private async Task<FileStream> OpenCatalogLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = catalogPath + ".lock";

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(lockPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DatasetAliasException(
                $"Could not open dataset alias catalog lock: {lockPath}. {ex.Message}",
                ex);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException ex) when (IsLockContention(ex))
            {
                await Task.Delay(LockRetryDelay, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new DatasetAliasException(
                    $"Could not open dataset alias catalog lock: {lockPath}. {ex.Message}",
                    ex);
            }
        }
    }

    /// <summary>
    /// Creates the default temporary path used for atomic catalog writes.
    /// </summary>
    /// <param name="finalCatalogPath">Final catalog path.</param>
    /// <returns>The temporary catalog path.</returns>
    private static string CreateTempCatalogPath(string finalCatalogPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(finalCatalogPath));
        var fileName = Path.GetFileName(finalCatalogPath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            return $"{fileName}.{Guid.NewGuid():N}.tmp";
        }

        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");
    }

    /// <summary>
    /// Checks whether an I/O error represents another process holding the catalog lock.
    /// </summary>
    /// <param name="exception">I/O exception to inspect.</param>
    /// <returns><see langword="true" /> when the exception represents lock contention.</returns>
    private static bool IsLockContention(IOException exception)
    {
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);

        return exception.HResult is sharingViolation or lockViolation;
    }

    /// <summary>
    /// Attempts to delete a temporary file without masking the original write failure.
    /// </summary>
    /// <param name="path">Temporary file path to delete.</param>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// JSON document wrapper for the local alias catalog.
    /// </summary>
    /// <param name="Datasets">Persisted dataset aliases.</param>
    private sealed record DatasetAliasDocument(IReadOnlyList<DatasetAlias> Datasets);
}
