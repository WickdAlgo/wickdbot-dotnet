#nullable enable

using WickdBot.Engines;
using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot.Tests;

/// <summary>
/// Verifies deterministic market-structure detection over replayed candle streams.
/// </summary>
public class StructureEngineTests
{
    /// <summary>
    /// Confirms structure processing rejects non-backtest candle streams.
    /// </summary>
    [Fact]
    public void ProcessRejectsNonBacktestCandles()
    {
        var engine = new StructureEngine();

        var exception = Assert.Throws<StructureException>(
            () => engine.Process(
                [CreateCandle(0, 100m, 101m, 99m, 100m, CandleSource.Historical, runId: null)],
                CreateSettings()));

        Assert.Contains("backtest-source candles", exception.Message);
    }

    /// <summary>
    /// Confirms structure processing rejects duplicate or non-increasing candle open times.
    /// </summary>
    [Fact]
    public void ProcessRejectsNonIncreasingOpenTimes()
    {
        var engine = new StructureEngine();
        var duplicateTime = CreateOpenTime(0);

        var exception = Assert.Throws<StructureException>(
            () => engine.Process(
                [
                    CreateCandle(duplicateTime, 100m, 101m, 99m, 100m),
                    CreateCandle(duplicateTime, 101m, 102m, 100m, 101m)
                ],
                CreateSettings()));

        Assert.Contains("strictly increasing", exception.Message);
    }

    /// <summary>
    /// Confirms higher-high continuation updates the same high candidate instead of finalizing multiple highs.
    /// </summary>
    [Fact]
    public void ProcessUpdatesHigherHighCandidateWithoutFinalizing()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 11m, 12m, 9m, 11m),
                CreateCandle(2, 12m, 14m, 10m, 13m)
            ],
            CreateSettings());

        var highUpdates = result.Events
            .Where(structureEvent => structureEvent.EventType == StructureEventType.SwingHighCandidateUpdated)
            .ToArray();

        Assert.Equal("swing-000001", Assert.Single(result.Events, e => e.EventType == StructureEventType.SwingHighCandidate).EntityId);
        Assert.All(highUpdates, update => Assert.Equal("swing-000001", update.EntityId));
        Assert.Equal(14m, highUpdates[^1].Price);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.SwingHighFinalized);
    }

    /// <summary>
    /// Confirms breaking a prior swing high finalizes the lowest low between the previous high and new high candidate.
    /// </summary>
    [Fact]
    public void ProcessFinalizesInterveningLowWhenPriorHighBreaks()
    {
        var result = new StructureEngine().Process(CreateHighBreakCandles(), CreateSettings());

        var finalizedHigh = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.SwingHighFinalized);
        var finalizedLow = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.SwingLowFinalized);
        var farRightHigh = result.Events
            .Last(structureEvent => structureEvent.EventType == StructureEventType.SwingHighCandidate);

        Assert.Equal("swing-000001", finalizedHigh.EntityId);
        Assert.Equal(15m, finalizedHigh.Price);
        Assert.Equal("swing-000003", finalizedLow.EntityId);
        Assert.Equal(7m, finalizedLow.Price);
        Assert.Equal("swing-000004", farRightHigh.EntityId);
        Assert.Equal(16m, farRightHigh.Price);
    }

    /// <summary>
    /// Confirms breaking a prior swing low finalizes the highest high between the previous low and new low candidate.
    /// </summary>
    [Fact]
    public void ProcessFinalizesInterveningHighWhenPriorLowBreaks()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 15m, 10m, 10m),
                CreateCandle(1, 9m, 11m, 8m, 9m),
                CreateCandle(2, 10m, 13m, 9m, 10m),
                CreateCandle(3, 8m, 10m, 7m, 8m)
            ],
            CreateSettings());

        var finalizedLow = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.SwingLowFinalized);
        var finalizedHigh = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.SwingHighFinalized);
        var farRightLow = result.Events
            .Last(structureEvent => structureEvent.EventType == StructureEventType.SwingLowCandidate);

        Assert.Equal("swing-000002", finalizedLow.EntityId);
        Assert.Equal(8m, finalizedLow.Price);
        Assert.Equal("swing-000003", finalizedHigh.EntityId);
        Assert.Equal(13m, finalizedHigh.Price);
        Assert.Equal("swing-000004", farRightLow.EntityId);
        Assert.Equal(7m, farRightLow.Price);
    }

    /// <summary>
    /// Confirms one-candle flip noise does not finalize swings when minimum separation is higher.
    /// </summary>
    [Fact]
    public void ProcessDoesNotFinalizeTooCloseInterveningSwing()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 10m, 14m, 7m, 10m),
                CreateCandle(3, 15m, 16m, 10m, 15m)
            ],
            CreateSettings(minimumSwingSeparationCandles: 2));

        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.SwingHighFinalized);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.SwingLowFinalized);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BuySideLiquidityBreached);
    }

    /// <summary>
    /// Confirms separated intervening swings still finalize when they satisfy the configured minimum distance.
    /// </summary>
    [Fact]
    public void ProcessFinalizesSeparatedInterveningSwing()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 11m, 14m, 8m, 11m),
                CreateCandle(3, 10m, 13m, 7m, 10m),
                CreateCandle(4, 12m, 14m, 9m, 12m),
                CreateCandle(5, 15m, 16m, 10m, 15m)
            ],
            CreateSettings(minimumSwingSeparationCandles: 2));

        var finalizedHigh = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.SwingHighFinalized);
        var finalizedLow = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.SwingLowFinalized);

        Assert.Equal(15m, finalizedHigh.Price);
        Assert.Equal(7m, finalizedLow.Price);
    }

    /// <summary>
    /// Confirms swing candidates do not create liquidity before finalization.
    /// </summary>
    [Fact]
    public void ProcessDoesNotCreateLiquidityFromCandidates()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 11m, 12m, 9m, 11m)
            ],
            CreateSettings());

        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.EqualHighLiquidity);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BuySideLiquidityBreached);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.SellSideLiquidityBreached);
    }

    /// <summary>
    /// Confirms finalized swing highs can create equal-high liquidity with configured basis-point tolerance.
    /// </summary>
    [Fact]
    public void ProcessDetectsEqualHighLiquidityFromFinalizedSwings()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 10m, 14m, 7m, 10m),
                CreateCandle(3, 14m, 15.005m, 10m, 15m),
                CreateCandle(4, 9m, 14m, 6m, 9m)
            ],
            CreateSettings(equalLevelToleranceBasisPoints: 5m));

        var liquidity = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.EqualHighLiquidity);

        Assert.Equal("liq-000001", liquidity.EntityId);
        Assert.Equal(["swing-000001", "swing-000004"], liquidity.RelatedEntityIds);
        Assert.NotNull(liquidity.DistanceBasisPoints);
        Assert.True(liquidity.DistanceBasisPoints <= 5m);
    }

    /// <summary>
    /// Confirms wick-through alone emits a breach without immediately classifying a sweep or breakout.
    /// </summary>
    [Fact]
    public void ProcessEmitsBreachOnlyForUnclassifiedLiquidityTake()
    {
        var result = new StructureEngine().Process(CreateHighBreakCandles(), CreateSettings());

        var breach = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BuySideLiquidityBreached);

        Assert.Equal("breach-000001", breach.EntityId);
        Assert.Equal("swing-000001", breach.BreachedLiquidityId);
        Assert.Equal("swing-000003", breach.ProtectedSwingId);
        Assert.Equal("breached", breach.ClassificationStage);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BuySideSweepCandidate);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BuySideBreakoutConfirmed);
    }

    /// <summary>
    /// Confirms closing back inside after a buy-side breach emits a sweep candidate.
    /// </summary>
    [Fact]
    public void ProcessEmitsSweepCandidateWhenPriceClosesBackInside()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 10m, 14m, 7m, 10m),
                CreateCandle(3, 14m, 16m, 10m, 14m)
            ],
            CreateSettings());

        var candidate = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BuySideSweepCandidate);

        Assert.Equal("breach-000001", candidate.EntityId);
        Assert.Equal("structure-000008", candidate.OriginalBreachEventId);
        Assert.Equal("sweepCandidate", candidate.ClassificationStage);
    }

    /// <summary>
    /// Confirms same-timeframe bearish displacement after a buy-side sweep candidate emits rejection confirmation.
    /// </summary>
    [Fact]
    public void ProcessEmitsRejectionConfirmationAfterSweepCandidate()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 10m, 14m, 7m, 10m),
                CreateCandle(3, 14m, 16m, 10m, 14m),
                CreateCandle(4, 14m, 14.5m, 8m, 8m)
            ],
            CreateSettings(expansionBodyToAverageRange: 1m));

        var rejection = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BuySideRejectionConfirmed);

        Assert.Equal("breach-000001", rejection.EntityId);
        Assert.Equal("rejectionConfirmed", rejection.ClassificationStage);
        Assert.Equal(StructureDirection.Bearish, rejection.Direction);
    }

    /// <summary>
    /// Confirms breaking the protected swing low structurally confirms a buy-side sweep.
    /// </summary>
    [Fact]
    public void ProcessConfirmsSweepWhenProtectedSwingBreaks()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 10m, 14m, 7m, 10m),
                CreateCandle(3, 14m, 16m, 10m, 14m),
                CreateCandle(4, 14m, 14.5m, 8m, 8m),
                CreateCandle(5, 8m, 9m, 6m, 7m)
            ],
            CreateSettings(expansionBodyToAverageRange: 1m));

        var sweep = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BuySideSweepConfirmed);

        Assert.Equal("breach-000001", sweep.EntityId);
        Assert.Equal("swing-000003", sweep.ProtectedSwingId);
        Assert.Equal("sweepConfirmed", sweep.ClassificationStage);
    }

    /// <summary>
    /// Confirms closing beyond taken liquidity confirms breakout before a sweep candidate forms.
    /// </summary>
    [Fact]
    public void ProcessConfirmsBreakoutWhenPriceAcceptsBeyondBreachedLevel()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 10m, 10m, 5m, 10m),
                CreateCandle(1, 12m, 15m, 9m, 12m),
                CreateCandle(2, 10m, 14m, 7m, 10m),
                CreateCandle(3, 15m, 16m, 10m, 15.5m)
            ],
            CreateSettings());

        var breakout = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BuySideBreakoutConfirmed);

        Assert.Equal("breach-000001", breakout.EntityId);
        Assert.Equal("breakoutConfirmed", breakout.ClassificationStage);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BuySideSweepCandidate);
    }

    /// <summary>
    /// Confirms bullish order blocks, expansion/FVG, FVG fills, and OB consumption are journaled deterministically.
    /// </summary>
    [Fact]
    public void ProcessDetectsBullishOrderBlockExpansionFvgAndLifecycle()
    {
        var result = new StructureEngine().Process(CreateBullishStructureCandles(), CreateSettings());

        var orderBlock = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BullishOrderBlockDiscovered);
        var expansion = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BullishExpansion);
        var fvg = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BullishFvgDiscovered);
        var fillUpdates = result.Events
            .Where(structureEvent => structureEvent.EventType == StructureEventType.FvgFillUpdated)
            .ToArray();
        var consumed = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.OrderBlockConsumed);

        Assert.Equal("ob-000001", orderBlock.EntityId);
        Assert.Equal(OrderBlockState.Active, orderBlock.OrderBlockState);
        Assert.Equal(["ob-000001"], expansion.RelatedEntityIds);
        Assert.Equal("fvg-000001", fvg.EntityId);
        Assert.Equal(101m, fvg.ZoneLow);
        Assert.Equal(102m, fvg.ZoneHigh);
        Assert.Equal(
            [50m, 100m],
            fillUpdates.Select(structureEvent => structureEvent.FillPercent.GetValueOrDefault()).ToArray());
        Assert.Equal(FvgState.Filled, fillUpdates[^1].FvgState);
        Assert.Equal(OrderBlockState.Consumed, consumed.OrderBlockState);
        Assert.Empty(result.Snapshot.ActiveOrderBlockIds);
        Assert.Empty(result.Snapshot.ActiveFvgIds);
    }

    /// <summary>
    /// Confirms bearish order blocks and bearish FVGs mirror the bullish path.
    /// </summary>
    [Fact]
    public void ProcessDetectsBearishOrderBlockAndFvg()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 100m, 100.5m, 99.5m, 100m),
                CreateCandle(1, 100m, 103m, 99m, 102m),
                CreateCandle(2, 101m, 102m, 93m, 94m),
                CreateCandle(3, 94m, 98m, 92m, 93m)
            ],
            CreateSettings());

        var orderBlock = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BearishOrderBlockDiscovered);
        var fvg = Assert.Single(
            result.Events,
            structureEvent => structureEvent.EventType == StructureEventType.BearishFvgDiscovered);

        Assert.Equal("ob-000001", orderBlock.EntityId);
        Assert.Equal(StructureDirection.Bearish, orderBlock.Direction);
        Assert.Equal("fvg-000001", fvg.EntityId);
        Assert.Equal(98m, fvg.ZoneLow);
        Assert.Equal(99m, fvg.ZoneHigh);
    }

    /// <summary>
    /// Confirms expansion detection requires the classic three-candle FVG.
    /// </summary>
    [Fact]
    public void ProcessDoesNotEmitExpansionWithoutRequiredFvg()
    {
        var result = new StructureEngine().Process(
            [
                CreateCandle(0, 100m, 100.5m, 99.5m, 100m),
                CreateCandle(1, 100m, 101m, 97m, 98m),
                CreateCandle(2, 100m, 109m, 99m, 108m),
                CreateCandle(3, 106m, 110m, 100.5m, 107m)
            ],
            CreateSettings());

        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BullishExpansion);
        Assert.DoesNotContain(result.Events, structureEvent => structureEvent.EventType == StructureEventType.BullishFvgDiscovered);
    }

    /// <summary>
    /// Confirms event sequences and IDs are stable for the same candle input.
    /// </summary>
    [Fact]
    public void ProcessAssignsDeterministicEventIds()
    {
        var first = new StructureEngine().Process(CreateBullishStructureCandles(), CreateSettings());
        var second = new StructureEngine().Process(CreateBullishStructureCandles(), CreateSettings());

        Assert.Equal(
            first.Events.Select(structureEvent => structureEvent.EventId),
            second.Events.Select(structureEvent => structureEvent.EventId));
        Assert.Equal(
            Enumerable.Range(1, first.EventCount),
            first.Events.Select(structureEvent => structureEvent.Sequence));
    }

    /// <summary>
    /// Creates candles that break a prior high after forming an intervening low.
    /// </summary>
    /// <returns>Backtest candles for a high-break structure sequence.</returns>
    private static IReadOnlyList<CandleEvent> CreateHighBreakCandles()
    {
        return
        [
            CreateCandle(0, 10m, 10m, 5m, 10m),
            CreateCandle(1, 12m, 15m, 9m, 12m),
            CreateCandle(2, 10m, 14m, 7m, 10m),
            CreateCandle(3, 15m, 16m, 10m, 15m)
        ];
    }

    /// <summary>
    /// Creates the bullish structure fixture used by lifecycle tests.
    /// </summary>
    /// <returns>Backtest candles that produce one bullish OB/FVG sequence.</returns>
    private static IReadOnlyList<CandleEvent> CreateBullishStructureCandles()
    {
        return
        [
            CreateCandle(0, 100m, 100.5m, 99.5m, 100m),
            CreateCandle(1, 100m, 101m, 97m, 98m),
            CreateCandle(2, 100m, 109m, 99m, 108m),
            CreateCandle(3, 106m, 110m, 102m, 107m),
            CreateCandle(4, 107m, 108m, 101.5m, 106m),
            CreateCandle(5, 106m, 107m, 100.5m, 105m)
        ];
    }

    /// <summary>
    /// Creates structure settings for compact unit-test candles.
    /// </summary>
    /// <param name="minimumSwingSeparationCandles">Minimum candle distance for swing finalization.</param>
    /// <param name="equalLevelToleranceBasisPoints">Equal-level tolerance override.</param>
    /// <param name="expansionBodyToAverageRange">Expansion body threshold override.</param>
    /// <returns>Validated structure settings.</returns>
    private static StructureSettings CreateSettings(
        int minimumSwingSeparationCandles = 1,
        decimal equalLevelToleranceBasisPoints = 5m,
        decimal expansionBodyToAverageRange = 1.5m)
    {
        return new StructureSettings(
            MinimumSwingSeparationCandles: minimumSwingSeparationCandles,
            EqualLevelToleranceBasisPoints: equalLevelToleranceBasisPoints,
            OrderBlockSearchBackCandles: 3,
            ExpansionLookbackCandles: 1,
            ExpansionBodyToAverageRange: expansionBodyToAverageRange,
            ExpansionFvgWindowCandles: 2);
    }

    /// <summary>
    /// Creates a deterministic backtest candle at a fixture index.
    /// </summary>
    /// <param name="index">Five-minute candle index.</param>
    /// <param name="open">Open price.</param>
    /// <param name="high">High price.</param>
    /// <param name="low">Low price.</param>
    /// <param name="close">Close price.</param>
    /// <param name="source">Candle source.</param>
    /// <param name="runId">Backtest run ID.</param>
    /// <returns>The candle event.</returns>
    private static CandleEvent CreateCandle(
        int index,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        CandleSource source = CandleSource.Backtest,
        string? runId = "structure-test")
    {
        return CreateCandle(CreateOpenTime(index), open, high, low, close, source, runId);
    }

    /// <summary>
    /// Creates a deterministic backtest candle at an explicit UTC open time.
    /// </summary>
    /// <param name="openTimeUtc">UTC candle open time.</param>
    /// <param name="open">Open price.</param>
    /// <param name="high">High price.</param>
    /// <param name="low">Low price.</param>
    /// <param name="close">Close price.</param>
    /// <param name="source">Candle source.</param>
    /// <param name="runId">Backtest run ID.</param>
    /// <returns>The candle event.</returns>
    private static CandleEvent CreateCandle(
        DateTimeOffset openTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        CandleSource source = CandleSource.Backtest,
        string? runId = "structure-test")
    {
        return new CandleEvent(
            openTimeUtc,
            "BTC_USDT_PERP",
            "binance",
            "5m",
            open,
            high,
            low,
            close,
            volume: 10m,
            source,
            runId);
    }

    /// <summary>
    /// Creates a UTC open time for a five-minute fixture index.
    /// </summary>
    /// <param name="index">Five-minute candle index.</param>
    /// <returns>The UTC open time.</returns>
    private static DateTimeOffset CreateOpenTime(int index)
    {
        return new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero)
            .AddMinutes(index * 5);
    }
}
