#nullable enable

using Wickd.Models;

namespace Wickd.Data;

/// <summary>
/// Loads, fetches, normalizes, caches, and replays deterministic historical candle input.
/// </summary>
public sealed class HistoricalDataSource
{
    /// <summary>
    /// Market data clients keyed by Wickd exchange ID.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IMarketDataClient> clientsByExchangeId;

    /// <summary>
    /// Clock used to reject requests that would include incomplete candles.
    /// </summary>
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a historical data source with supported exchange clients.
    /// </summary>
    /// <param name="marketDataClients">Market data clients keyed by their declared exchange ID.</param>
    /// <param name="timeProvider">Clock used for completed-candle validation.</param>
    public HistoricalDataSource(
        IEnumerable<IMarketDataClient> marketDataClients,
        TimeProvider? timeProvider = null)
    {
        clientsByExchangeId = marketDataClients.ToDictionary(
            client => client.ExchangeId,
            StringComparer.Ordinal);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Loads an existing candle cache or fetches and writes the cache on miss.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    /// <param name="cancellationToken">Cancellation token for file and network I/O.</param>
    /// <returns>The normalized historical data result.</returns>
    /// <exception cref="WickdDataException">Thrown when the request, cache, exchange, or candle data is invalid.</exception>
    public async Task<HistoricalDataResult> LoadOrFetchAsync(
        RunRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCompletedRange(request);

        if (File.Exists(request.CandleCachePath))
        {
            var cachedCandles = await ReadHistoricalCacheAsync(request, cancellationToken);
            var cachedNormalization = NormalizeCandles(request, cachedCandles);
            EnsureAnyCandles(cachedNormalization.Candles, request);

            return new HistoricalDataResult(
                request,
                cachedNormalization.Candles,
                cachedNormalization.Gaps,
                request.CandleCachePath,
                CacheHit: true);
        }

        var client = ResolveClient(request.Market.ExchangeId);
        var exchangeCandles = await client.FetchCandlesAsync(
            request.Market,
            request.Timeframe,
            request.DateRange,
            cancellationToken);
        var historicalCandles = exchangeCandles
            .Where(candle => IsWithinRange(candle.OpenTimeUtc, request.DateRange))
            .Select(candle => ToHistoricalCandle(request, candle))
            .ToArray();
        var normalization = NormalizeCandles(request, historicalCandles);
        EnsureAnyCandles(normalization.Candles, request);

        try
        {
            await CandleJsonLines.WriteAsync(
                request.CandleCachePath,
                normalization.Candles,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WickdDataException(
                $"Could not write candle cache: {request.CandleCachePath}. {ex.Message}",
                ex);
        }

        return new HistoricalDataResult(
            request,
            normalization.Candles,
            normalization.Gaps,
            request.CandleCachePath,
            CacheHit: false);
    }

    /// <summary>
    /// Replays cached historical candles as backtest-source candles for a run.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    /// <param name="runId">Run ID to assign to replayed candles.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The replay result.</returns>
    /// <exception cref="WickdDataException">Thrown when the cache is missing or invalid.</exception>
    public async Task<CandleReplayResult> ReplayAsync(
        RunRequest request,
        string runId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new WickdDataException("Backtest run ID is required.");
        }

        ValidateCompletedRange(request);

        if (!File.Exists(request.CandleCachePath))
        {
            throw new WickdDataException(
                $"Candle cache was not found: {request.CandleCachePath}. Run fetch before backtest.");
        }

        var cachedCandles = await ReadHistoricalCacheAsync(request, cancellationToken);
        var normalization = NormalizeCandles(request, cachedCandles);
        EnsureAnyCandles(normalization.Candles, request);
        var replayedCandles = normalization.Candles
            .Select(candle => ToBacktestCandle(candle, runId))
            .ToArray();

        return new CandleReplayResult(
            runId,
            request.CandleCachePath,
            replayedCandles,
            normalization.Gaps);
    }

    /// <summary>
    /// Resolves the client for a configured exchange ID.
    /// </summary>
    /// <param name="exchangeId">Configured Wickd exchange ID.</param>
    /// <returns>The market data client.</returns>
    /// <exception cref="WickdDataException">Thrown when no Phase 2 client supports the exchange.</exception>
    private IMarketDataClient ResolveClient(string exchangeId)
    {
        if (clientsByExchangeId.TryGetValue(exchangeId, out var client))
        {
            return client;
        }

        throw new WickdDataException(
            $"Exchange '{exchangeId}' is not supported for historical data in Phase 2.");
    }

    /// <summary>
    /// Reads and validates the historical cache for a request.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>Historical candles read from cache.</returns>
    private static async Task<IReadOnlyList<CandleEvent>> ReadHistoricalCacheAsync(
        RunRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var candles = await CandleJsonLines.ReadAsync(request.CandleCachePath, cancellationToken);
            foreach (var candle in candles)
            {
                ValidateCachedHistoricalCandle(request, candle);
            }

            return candles;
        }
        catch (Exception ex) when (ex is not WickdDataException and not OperationCanceledException)
        {
            throw new WickdDataException(
                $"Candle cache is invalid: {request.CandleCachePath}. {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Validates one cached historical candle against the request identity.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    /// <param name="candle">Cached candle to validate.</param>
    private static void ValidateCachedHistoricalCandle(RunRequest request, CandleEvent candle)
    {
        if (candle.MarketId != request.Market.MarketId)
        {
            throw new WickdDataException(
                $"Cached candle market '{candle.MarketId}' does not match requested market '{request.Market.MarketId}'.");
        }

        if (candle.ExchangeId != request.Market.ExchangeId)
        {
            throw new WickdDataException(
                $"Cached candle exchange '{candle.ExchangeId}' does not match requested exchange '{request.Market.ExchangeId}'.");
        }

        if (candle.Timeframe != request.Timeframe.Value)
        {
            throw new WickdDataException(
                $"Cached candle timeframe '{candle.Timeframe}' does not match requested timeframe '{request.Timeframe.Value}'.");
        }

        if (candle.Source != CandleSource.Historical)
        {
            throw new WickdDataException("Candle cache must contain historical-source candles.");
        }

        if (candle.RunId is not null)
        {
            throw new WickdDataException("Historical candle cache must not contain run IDs.");
        }

        if (!IsWithinRange(candle.OpenTimeUtc, request.DateRange))
        {
            throw new WickdDataException(
                $"Cached candle open time {candle.OpenTimeUtc:O} is outside requested range {request.DateRange.FromUtc:O} to {request.DateRange.ToUtc:O}.");
        }
    }

    /// <summary>
    /// Converts one exchange candle into a normalized Wickd historical candle.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    /// <param name="candle">Raw exchange candle.</param>
    /// <returns>The Wickd historical candle.</returns>
    private static CandleEvent ToHistoricalCandle(RunRequest request, ExchangeCandle candle)
    {
        return new CandleEvent(
            candle.OpenTimeUtc,
            request.Market.MarketId,
            request.Market.ExchangeId,
            request.Timeframe.Value,
            candle.Open,
            candle.High,
            candle.Low,
            candle.Close,
            candle.Volume,
            CandleSource.Historical);
    }

    /// <summary>
    /// Converts a cached historical candle into a replayed backtest candle.
    /// </summary>
    /// <param name="candle">Historical candle to replay.</param>
    /// <param name="runId">Run ID to assign.</param>
    /// <returns>The backtest-source candle.</returns>
    private static CandleEvent ToBacktestCandle(CandleEvent candle, string runId)
    {
        return new CandleEvent(
            candle.OpenTimeUtc,
            candle.MarketId,
            candle.ExchangeId,
            candle.Timeframe,
            candle.Open,
            candle.High,
            candle.Low,
            candle.Close,
            candle.Volume,
            CandleSource.Backtest,
            runId);
    }

    /// <summary>
    /// Normalizes candle data and converts normalization failures into data exceptions.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    /// <param name="candles">Candles to normalize.</param>
    /// <returns>The normalization result.</returns>
    private static CandleNormalizationResult NormalizeCandles(
        RunRequest request,
        IEnumerable<CandleEvent> candles)
    {
        try
        {
            return CandleNormalizer.Normalize(candles, request.Timeframe);
        }
        catch (ArgumentException ex)
        {
            throw new WickdDataException($"Candle data is invalid: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ensures a request produced at least one candle.
    /// </summary>
    /// <param name="candles">Normalized candles.</param>
    /// <param name="request">Resolved historical run request.</param>
    private static void EnsureAnyCandles(IReadOnlyCollection<CandleEvent> candles, RunRequest request)
    {
        if (candles.Count == 0)
        {
            throw new WickdDataException(
                $"No candles were available for {request.Market.MarketId} {request.Timeframe.Value} from {request.DateRange.FromUtc:O} to {request.DateRange.ToUtc:O}.");
        }
    }

    /// <summary>
    /// Checks whether a candle open time belongs to the request's exclusive range.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <param name="dateRange">Requested UTC date range.</param>
    /// <returns><see langword="true" /> when the open time is inside the range.</returns>
    private static bool IsWithinRange(DateTimeOffset openTimeUtc, DateRange dateRange)
    {
        return openTimeUtc >= dateRange.FromUtc && openTimeUtc < dateRange.ToUtc;
    }

    /// <summary>
    /// Rejects requests whose exclusive end time would include incomplete candles.
    /// </summary>
    /// <param name="request">Resolved historical run request.</param>
    private void ValidateCompletedRange(RunRequest request)
    {
        var latestCompletedBoundaryUtc = FloorToTimeframeBoundary(
            timeProvider.GetUtcNow(),
            request.Timeframe.Duration);

        if (request.DateRange.ToUtc > latestCompletedBoundaryUtc)
        {
            throw new WickdDataException(
                $"Requested end time {request.DateRange.ToUtc:O} includes incomplete candles. Latest completed {request.Timeframe.Value} boundary is {latestCompletedBoundaryUtc:O}.");
        }
    }

    /// <summary>
    /// Floors a UTC instant to the nearest candle boundary.
    /// </summary>
    /// <param name="instantUtc">UTC instant to floor.</param>
    /// <param name="duration">Candle duration.</param>
    /// <returns>The floored UTC candle boundary.</returns>
    private static DateTimeOffset FloorToTimeframeBoundary(DateTimeOffset instantUtc, TimeSpan duration)
    {
        var utc = instantUtc.ToUniversalTime();
        var ticks = utc.Ticks - (utc.Ticks % duration.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
