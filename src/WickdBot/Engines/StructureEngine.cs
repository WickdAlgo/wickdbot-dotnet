#nullable enable

using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot.Engines;

/// <summary>
/// Detects deterministic market-structure events from one replayed candle stream.
/// </summary>
internal sealed class StructureEngine
{
    /// <summary>
    /// Processes replayed backtest candles into ordered structure events.
    /// </summary>
    /// <param name="candles">Replayed backtest-source candles for one run.</param>
    /// <param name="settings">Validated structure detection settings.</param>
    /// <returns>The emitted structure events and final snapshot.</returns>
    /// <exception cref="StructureException">Thrown when candle stream assumptions are violated.</exception>
    internal StructureProcessingResult Process(
        IReadOnlyList<CandleEvent> candles,
        StructureSettings settings)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(settings);

        var identity = ValidateCandleStream(candles);
        var state = new ProcessingState(candles, settings, identity);

        for (var index = 0; index < candles.Count; index++)
        {
            state.UpdateSwingSequence(index);
            state.UpdateLiquidityLifecycle(index);
            state.UpdateFvgFills(index);
            state.UpdateOrderBlockLifecycle(index);
            state.DiscoverOrderBlocks(index);
            state.DetectExpansionFvgs(index);
        }

        return new StructureProcessingResult(
            identity.RunId,
            state.Events,
            state.CreateSnapshot());
    }

    /// <summary>
    /// Validates that candles satisfy the single-run structure processing contract.
    /// </summary>
    /// <param name="candles">Candles to validate.</param>
    /// <returns>The shared stream identity.</returns>
    /// <exception cref="StructureException">Thrown when the stream is empty, mixed, unordered, or not a backtest stream.</exception>
    private static CandleStreamIdentity ValidateCandleStream(IReadOnlyList<CandleEvent> candles)
    {
        if (candles.Count == 0)
        {
            throw new StructureException("Structure processing requires at least one candle.");
        }

        var first = candles[0];
        if (first.Source != CandleSource.Backtest)
        {
            throw new StructureException("Structure processing requires backtest-source candles.");
        }

        if (string.IsNullOrWhiteSpace(first.RunId))
        {
            throw new StructureException("Structure processing requires candles with a backtest run ID.");
        }

        for (var index = 0; index < candles.Count; index++)
        {
            var candle = candles[index];
            if (candle.Source != CandleSource.Backtest)
            {
                throw new StructureException("Structure processing requires backtest-source candles.");
            }

            if (candle.RunId != first.RunId)
            {
                throw new StructureException("Structure processing requires exactly one run ID.");
            }

            if (candle.MarketId != first.MarketId
                || candle.ExchangeId != first.ExchangeId
                || candle.Timeframe != first.Timeframe)
            {
                throw new StructureException("Structure processing requires one market, exchange, and timeframe per run.");
            }

            if (candle.OpenTimeUtc.Offset != TimeSpan.Zero)
            {
                throw new StructureException("Structure processing requires UTC candle open times.");
            }

            if (index > 0 && candle.OpenTimeUtc <= candles[index - 1].OpenTimeUtc)
            {
                throw new StructureException("Structure processing requires strictly increasing candle open times.");
            }
        }

        return new CandleStreamIdentity(
            first.RunId,
            first.MarketId,
            first.ExchangeId,
            first.Timeframe);
    }

    private enum SwingKind
    {
        High,
        Low
    }

    private enum LiquiditySide
    {
        BuySide,
        SellSide
    }

    private enum LiquidityBreachStage
    {
        Breached,
        SweepCandidate,
        RejectionConfirmed,
        SweepConfirmed,
        BreakoutConfirmed
    }

    private sealed record CandleStreamIdentity(
        string RunId,
        string MarketId,
        string ExchangeId,
        string Timeframe);

    private sealed class SwingPoint
    {
        internal SwingPoint(
            string id,
            SwingKind kind,
            int index,
            decimal price)
        {
            Id = id;
            Kind = kind;
            Index = index;
            Price = price;
        }

        internal string Id { get; }

        internal SwingKind Kind { get; }

        internal int Index { get; private set; }

        internal decimal Price { get; private set; }

        internal bool Finalized { get; private set; }

        internal void MoveTo(int index, decimal price)
        {
            Index = index;
            Price = price;
        }

        internal void MarkFinalized()
        {
            Finalized = true;
        }
    }

    private sealed class LiquidityLevel
    {
        internal LiquidityLevel(
            string id,
            LiquiditySide side,
            int observedIndex,
            decimal zoneLow,
            decimal zoneHigh,
            string[] relatedSwingIds)
        {
            Id = id;
            Side = side;
            ObservedIndex = observedIndex;
            ZoneLow = zoneLow;
            ZoneHigh = zoneHigh;
            RelatedSwingIds = relatedSwingIds;
        }

        internal string Id { get; }

        internal LiquiditySide Side { get; }

        internal int ObservedIndex { get; }

        internal decimal ZoneLow { get; }

        internal decimal ZoneHigh { get; }

        internal string[] RelatedSwingIds { get; }

        internal bool Classified { get; set; }
    }

    private sealed class LiquidityBreach
    {
        internal LiquidityBreach(
            string id,
            string breachEventId,
            LiquidityLevel level,
            int observedIndex,
            string? protectedSwingId,
            decimal? protectedSwingPrice)
        {
            Id = id;
            BreachEventId = breachEventId;
            Level = level;
            ObservedIndex = observedIndex;
            ProtectedSwingId = protectedSwingId;
            ProtectedSwingPrice = protectedSwingPrice;
        }

        internal string Id { get; }

        internal string BreachEventId { get; }

        internal LiquidityLevel Level { get; }

        internal int ObservedIndex { get; }

        internal string? ProtectedSwingId { get; }

        internal decimal? ProtectedSwingPrice { get; }

        internal LiquidityBreachStage Stage { get; set; } = LiquidityBreachStage.Breached;

        internal int? CandidateIndex { get; set; }

        internal bool IsTerminal => Stage is LiquidityBreachStage.SweepConfirmed or LiquidityBreachStage.BreakoutConfirmed;
    }

    private sealed class OrderBlockInfo
    {
        internal OrderBlockInfo(
            string id,
            StructureDirection direction,
            int subjectIndex,
            int discoveryIndex,
            decimal zoneLow,
            decimal zoneHigh)
        {
            Id = id;
            Direction = direction;
            SubjectIndex = subjectIndex;
            DiscoveryIndex = discoveryIndex;
            ZoneLow = zoneLow;
            ZoneHigh = zoneHigh;
        }

        internal string Id { get; }

        internal StructureDirection Direction { get; }

        internal int SubjectIndex { get; }

        internal int DiscoveryIndex { get; }

        internal decimal ZoneLow { get; }

        internal decimal ZoneHigh { get; }

        internal bool Consumed { get; set; }
    }

    private sealed class FvgInfo
    {
        internal FvgInfo(
            string id,
            string orderBlockId,
            StructureDirection direction,
            int middleIndex,
            int discoveryIndex,
            decimal zoneLow,
            decimal zoneHigh)
        {
            Id = id;
            OrderBlockId = orderBlockId;
            Direction = direction;
            MiddleIndex = middleIndex;
            DiscoveryIndex = discoveryIndex;
            ZoneLow = zoneLow;
            ZoneHigh = zoneHigh;
        }

        internal string Id { get; }

        internal string OrderBlockId { get; }

        internal StructureDirection Direction { get; }

        internal int MiddleIndex { get; }

        internal int DiscoveryIndex { get; }

        internal decimal ZoneLow { get; }

        internal decimal ZoneHigh { get; }

        internal decimal FillPercent { get; set; }

        internal bool Filled { get; set; }
    }

    private sealed class ProcessingState
    {
        private readonly IReadOnlyList<CandleEvent> candles;
        private readonly StructureSettings settings;
        private readonly CandleStreamIdentity identity;
        private readonly List<StructureEvent> events = [];
        private readonly List<SwingPoint> finalizedSwings = [];
        private readonly List<LiquidityLevel> liquidityLevels = [];
        private readonly List<LiquidityBreach> liquidityBreaches = [];
        private readonly List<OrderBlockInfo> orderBlocks = [];
        private readonly List<FvgInfo> fvgs = [];
        private readonly List<LiquidityLevel> unclassifiedLiquidityLevels = [];
        private readonly List<LiquidityBreach> openLiquidityBreaches = [];
        private readonly List<OrderBlockInfo> activeOrderBlocks = [];
        private readonly List<FvgInfo> activeFvgs = [];
        private readonly HashSet<string> discoveredOrderBlocks = new(StringComparer.Ordinal);
        private readonly HashSet<string> discoveredFvgs = new(StringComparer.Ordinal);
        private readonly decimal[] candleRangePrefixSums;
        private int eventSequence;
        private int swingSequence;
        private int liquiditySequence;
        private int breachSequence;
        private int orderBlockSequence;
        private int fvgSequence;
        private SwingPoint? activeHighCandidate;
        private SwingPoint? activeLowCandidate;
        private SwingPoint? lowBetweenHighsCandidate;
        private SwingPoint? highBetweenLowsCandidate;

        internal ProcessingState(
            IReadOnlyList<CandleEvent> candles,
            StructureSettings settings,
            CandleStreamIdentity identity)
        {
            this.candles = candles;
            this.settings = settings;
            this.identity = identity;
            candleRangePrefixSums = BuildCandleRangePrefixSums(candles);
        }

        internal IReadOnlyList<StructureEvent> Events => events;

        internal void UpdateSwingSequence(int index)
        {
            var candle = candles[index];
            if (index == 0)
            {
                activeHighCandidate = CreateSwingCandidate(SwingKind.High, index, candle.High);
                activeLowCandidate = CreateSwingCandidate(SwingKind.Low, index, candle.Low);
                return;
            }

            var highBreakAnchor = activeHighCandidate ?? LastFinalizedSwing(SwingKind.High);
            var lowBreakAnchor = activeLowCandidate ?? LastFinalizedSwing(SwingKind.Low);
            var highBreak = highBreakAnchor is not null && candle.High > highBreakAnchor.Price;
            var lowBreak = lowBreakAnchor is not null && candle.Low < lowBreakAnchor.Price;

            if (highBreak && lowBreak)
            {
                if (candle.Close >= candle.Open)
                {
                    HandleHighBreak(index);
                }
                else
                {
                    HandleLowBreak(index);
                }

                return;
            }

            if (highBreak)
            {
                HandleHighBreak(index);
            }
            else if (!lowBreak)
            {
                TrackLowBetweenHighs(index);
            }

            if (lowBreak)
            {
                HandleLowBreak(index);
            }
            else if (!highBreak)
            {
                TrackHighBetweenLows(index);
            }
        }

        internal void UpdateLiquidityLifecycle(int index)
        {
            EmitLiquidityBreaches(index);
            UpdateOpenLiquidityBreaches(index);
        }

        internal void UpdateFvgFills(int index)
        {
            var candle = candles[index];
            for (var position = 0; position < activeFvgs.Count;)
            {
                var fvg = activeFvgs[position];
                if (fvg.DiscoveryIndex >= index)
                {
                    position++;
                    continue;
                }

                var fillPercent = CalculateFillPercent(fvg, candle);
                if (fillPercent <= fvg.FillPercent)
                {
                    position++;
                    continue;
                }

                fvg.FillPercent = fillPercent;
                fvg.Filled = fillPercent >= 100m;
                Emit(
                    StructureEventType.FvgFillUpdated,
                    observedIndex: index,
                    subjectIndex: index,
                    entityId: fvg.Id,
                    relatedEntityIds: [fvg.OrderBlockId],
                    direction: fvg.Direction,
                    zoneLow: fvg.ZoneLow,
                    zoneHigh: fvg.ZoneHigh,
                    fillPercent: fillPercent,
                    fvgState: fvg.Filled ? FvgState.Filled : FvgState.Active,
                    sourceIndices: [index]);

                if (fvg.Filled)
                {
                    activeFvgs.RemoveAt(position);
                    continue;
                }

                position++;
            }
        }

        internal void UpdateOrderBlockLifecycle(int index)
        {
            var candle = candles[index];
            for (var position = 0; position < activeOrderBlocks.Count;)
            {
                var orderBlock = activeOrderBlocks[position];
                if (orderBlock.DiscoveryIndex >= index)
                {
                    position++;
                    continue;
                }

                if (!IntersectsZone(candle, orderBlock.ZoneLow, orderBlock.ZoneHigh))
                {
                    position++;
                    continue;
                }

                Emit(
                    StructureEventType.OrderBlockMitigated,
                    observedIndex: index,
                    subjectIndex: index,
                    entityId: orderBlock.Id,
                    direction: orderBlock.Direction,
                    zoneLow: orderBlock.ZoneLow,
                    zoneHigh: orderBlock.ZoneHigh,
                    orderBlockState: OrderBlockState.Mitigated,
                    sourceIndices: [index]);
                Emit(
                    StructureEventType.OrderBlockConsumed,
                    observedIndex: index,
                    subjectIndex: index,
                    entityId: orderBlock.Id,
                    direction: orderBlock.Direction,
                    zoneLow: orderBlock.ZoneLow,
                    zoneHigh: orderBlock.ZoneHigh,
                    orderBlockState: OrderBlockState.Consumed,
                    sourceIndices: [index]);
                orderBlock.Consumed = true;
                activeOrderBlocks.RemoveAt(position);
                continue;
            }
        }

        internal void DiscoverOrderBlocks(int index)
        {
            TryDiscoverOrderBlock(index, StructureDirection.Bullish);
            TryDiscoverOrderBlock(index, StructureDirection.Bearish);
        }

        internal void DetectExpansionFvgs(int observedIndex)
        {
            if (observedIndex < 2)
            {
                return;
            }

            var middleIndex = observedIndex - 1;
            if (middleIndex < settings.ExpansionLookbackCandles)
            {
                return;
            }

            TryDetectExpansionFvg(observedIndex, middleIndex, StructureDirection.Bullish);
            TryDetectExpansionFvg(observedIndex, middleIndex, StructureDirection.Bearish);
        }

        internal StructureSnapshot CreateSnapshot()
        {
            return new StructureSnapshot(
                identity.RunId,
                identity.MarketId,
                identity.ExchangeId,
                identity.Timeframe,
                activeOrderBlocks
                    .Select(orderBlock => orderBlock.Id)
                    .ToArray(),
                activeFvgs
                    .Select(fvg => fvg.Id)
                    .ToArray());
        }

        private void HandleHighBreak(int index)
        {
            var candle = candles[index];
            var previousFinalizedHigh = LastFinalizedSwing(SwingKind.High);
            if (previousFinalizedHigh is not null && LastFinalizedSwing()?.Kind == SwingKind.High)
            {
                if (activeHighCandidate is null)
                {
                    activeHighCandidate = CreateSwingCandidate(SwingKind.High, index, candle.High);
                }
                else
                {
                    UpdateSwingCandidate(activeHighCandidate, index, candle.High);
                }

                highBetweenLowsCandidate = activeHighCandidate;
                TryFinalizeLowBetweenHighs(previousFinalizedHigh, activeHighCandidate, index);
                return;
            }

            if (activeHighCandidate is null)
            {
                activeHighCandidate = CreateSwingCandidate(SwingKind.High, index, candle.High);
                return;
            }

            if (lowBetweenHighsCandidate is null)
            {
                UpdateSwingCandidate(activeHighCandidate, index, candle.High);
                highBetweenLowsCandidate = activeHighCandidate;
                return;
            }

            if (!HasMinimumSwingSeparation(activeHighCandidate, lowBetweenHighsCandidate, index))
            {
                UpdateSwingCandidate(activeHighCandidate, index, candle.High);
                highBetweenLowsCandidate = activeHighCandidate;
                lowBetweenHighsCandidate = null;
                return;
            }

            _ = FinalizeSwing(activeHighCandidate, index);
            if (FinalizeSwing(lowBetweenHighsCandidate, index))
            {
                activeLowCandidate = null;
                lowBetweenHighsCandidate = null;
            }

            activeHighCandidate = CreateSwingCandidate(SwingKind.High, index, candle.High);
            highBetweenLowsCandidate = activeHighCandidate;
        }

        private void HandleLowBreak(int index)
        {
            var candle = candles[index];
            var previousFinalizedLow = LastFinalizedSwing(SwingKind.Low);
            if (previousFinalizedLow is not null && LastFinalizedSwing()?.Kind == SwingKind.Low)
            {
                if (activeLowCandidate is null)
                {
                    activeLowCandidate = CreateSwingCandidate(SwingKind.Low, index, candle.Low);
                }
                else
                {
                    UpdateSwingCandidate(activeLowCandidate, index, candle.Low);
                }

                lowBetweenHighsCandidate = activeLowCandidate;
                TryFinalizeHighBetweenLows(previousFinalizedLow, activeLowCandidate, index);
                return;
            }

            if (activeLowCandidate is null)
            {
                activeLowCandidate = CreateSwingCandidate(SwingKind.Low, index, candle.Low);
                return;
            }

            if (highBetweenLowsCandidate is null)
            {
                UpdateSwingCandidate(activeLowCandidate, index, candle.Low);
                lowBetweenHighsCandidate = activeLowCandidate;
                return;
            }

            if (!HasMinimumSwingSeparation(activeLowCandidate, highBetweenLowsCandidate, index))
            {
                UpdateSwingCandidate(activeLowCandidate, index, candle.Low);
                lowBetweenHighsCandidate = activeLowCandidate;
                highBetweenLowsCandidate = null;
                return;
            }

            _ = FinalizeSwing(activeLowCandidate, index);
            if (FinalizeSwing(highBetweenLowsCandidate, index))
            {
                activeHighCandidate = null;
                highBetweenLowsCandidate = null;
            }

            activeLowCandidate = CreateSwingCandidate(SwingKind.Low, index, candle.Low);
            lowBetweenHighsCandidate = activeLowCandidate;
        }

        private void TrackLowBetweenHighs(int index)
        {
            var highAnchor = activeHighCandidate ?? LastFinalizedSwing(SwingKind.High);
            if (highAnchor is null || index <= highAnchor.Index)
            {
                return;
            }

            var low = candles[index].Low;
            if (lowBetweenHighsCandidate is null)
            {
                lowBetweenHighsCandidate = CreateSwingCandidate(SwingKind.Low, index, low);
            }
            else if (low < lowBetweenHighsCandidate.Price)
            {
                UpdateSwingCandidate(lowBetweenHighsCandidate, index, low);
            }
        }

        private void TrackHighBetweenLows(int index)
        {
            var lowAnchor = activeLowCandidate ?? LastFinalizedSwing(SwingKind.Low);
            if (lowAnchor is null || index <= lowAnchor.Index)
            {
                return;
            }

            var high = candles[index].High;
            if (highBetweenLowsCandidate is null)
            {
                highBetweenLowsCandidate = CreateSwingCandidate(SwingKind.High, index, high);
            }
            else if (high > highBetweenLowsCandidate.Price)
            {
                UpdateSwingCandidate(highBetweenLowsCandidate, index, high);
            }
        }

        private void TryFinalizeLowBetweenHighs(
            SwingPoint previousFinalizedHigh,
            SwingPoint currentHighCandidate,
            int observedIndex)
        {
            if (lowBetweenHighsCandidate is null
                || !HasMinimumSwingSeparation(previousFinalizedHigh, lowBetweenHighsCandidate, currentHighCandidate.Index))
            {
                return;
            }

            if (FinalizeSwing(lowBetweenHighsCandidate, observedIndex))
            {
                activeLowCandidate = null;
                lowBetweenHighsCandidate = null;
            }
        }

        private void TryFinalizeHighBetweenLows(
            SwingPoint previousFinalizedLow,
            SwingPoint currentLowCandidate,
            int observedIndex)
        {
            if (highBetweenLowsCandidate is null
                || !HasMinimumSwingSeparation(previousFinalizedLow, highBetweenLowsCandidate, currentLowCandidate.Index))
            {
                return;
            }

            if (FinalizeSwing(highBetweenLowsCandidate, observedIndex))
            {
                activeHighCandidate = null;
                highBetweenLowsCandidate = null;
            }
        }

        /// <summary>
        /// Determines whether an intervening swing is far enough from both same-side anchors to finalize.
        /// </summary>
        /// <param name="sameSideAnchor">The prior same-side swing candidate.</param>
        /// <param name="interveningOppositeSwing">The opposite swing candidate between same-side anchors.</param>
        /// <param name="currentSameSideIndex">The current candle index that broke the same-side anchor.</param>
        /// <returns><see langword="true"/> when the intervening swing satisfies the configured separation.</returns>
        private bool HasMinimumSwingSeparation(
            SwingPoint sameSideAnchor,
            SwingPoint interveningOppositeSwing,
            int currentSameSideIndex)
        {
            var minimum = settings.MinimumSwingSeparationCandles;
            return interveningOppositeSwing.Index - sameSideAnchor.Index >= minimum
                && currentSameSideIndex - interveningOppositeSwing.Index >= minimum;
        }

        private SwingPoint CreateSwingCandidate(SwingKind kind, int index, decimal price)
        {
            var swing = new SwingPoint(NextSwingId(), kind, index, price);
            Emit(
                kind == SwingKind.High
                    ? StructureEventType.SwingHighCandidate
                    : StructureEventType.SwingLowCandidate,
                observedIndex: index,
                subjectIndex: index,
                entityId: swing.Id,
                price: price,
                classificationStage: "candidate",
                sourceIndices: [index]);

            return swing;
        }

        private void UpdateSwingCandidate(SwingPoint swing, int index, decimal price)
        {
            if (swing.Finalized)
            {
                throw new StructureException($"Finalized swing '{swing.Id}' cannot be updated as a candidate.");
            }

            if (swing.Index == index && swing.Price == price)
            {
                return;
            }

            swing.MoveTo(index, price);
            Emit(
                swing.Kind == SwingKind.High
                    ? StructureEventType.SwingHighCandidateUpdated
                    : StructureEventType.SwingLowCandidateUpdated,
                observedIndex: index,
                subjectIndex: index,
                entityId: swing.Id,
                price: price,
                classificationStage: "candidateUpdated",
                sourceIndices: [index]);
        }

        private bool FinalizeSwing(SwingPoint swing, int observedIndex)
        {
            if (swing.Finalized)
            {
                return false;
            }

            swing.MarkFinalized();
            finalizedSwings.Add(swing);
            Emit(
                swing.Kind == SwingKind.High
                    ? StructureEventType.SwingHighFinalized
                    : StructureEventType.SwingLowFinalized,
                observedIndex,
                swing.Index,
                entityId: swing.Id,
                price: swing.Price,
                classificationStage: "finalized",
                sourceIndices: [swing.Index, observedIndex]);
            AddLiquidityFromFinalSwing(swing, observedIndex);
            return true;
        }

        private void AddLiquidityFromFinalSwing(SwingPoint swing, int observedIndex)
        {
            AddLiquidityLevel(new LiquidityLevel(
                swing.Id,
                swing.Kind == SwingKind.High ? LiquiditySide.BuySide : LiquiditySide.SellSide,
                observedIndex,
                swing.Price,
                swing.Price,
                [swing.Id]));
            TryEmitEqualLiquidity(swing, observedIndex);
        }

        private void TryEmitEqualLiquidity(SwingPoint current, int observedIndex)
        {
            var previous = PreviousFinalizedSwing(current);
            if (previous is null)
            {
                return;
            }

            var distanceBasisPoints = CalculateDistanceBasisPoints(previous.Price, current.Price);
            if (distanceBasisPoints > settings.EqualLevelToleranceBasisPoints)
            {
                return;
            }

            var id = NextLiquidityId();
            var zoneLow = Math.Min(previous.Price, current.Price);
            var zoneHigh = Math.Max(previous.Price, current.Price);
            var eventType = current.Kind == SwingKind.High
                ? StructureEventType.EqualHighLiquidity
                : StructureEventType.EqualLowLiquidity;

            Emit(
                eventType,
                observedIndex,
                current.Index,
                entityId: id,
                relatedEntityIds: [previous.Id, current.Id],
                price: (previous.Price + current.Price) / 2m,
                zoneLow: zoneLow,
                zoneHigh: zoneHigh,
                distanceBasisPoints: distanceBasisPoints,
                classificationStage: "liquidityFinalized",
                sourceIndices: [previous.Index, current.Index]);

            AddLiquidityLevel(new LiquidityLevel(
                id,
                current.Kind == SwingKind.High ? LiquiditySide.BuySide : LiquiditySide.SellSide,
                observedIndex,
                zoneLow,
                zoneHigh,
                [previous.Id, current.Id]));
        }

        private void AddLiquidityLevel(LiquidityLevel level)
        {
            liquidityLevels.Add(level);
            unclassifiedLiquidityLevels.Add(level);
        }

        private void EmitLiquidityBreaches(int index)
        {
            var candle = candles[index];
            for (var position = 0; position < unclassifiedLiquidityLevels.Count;)
            {
                var level = unclassifiedLiquidityLevels[position];
                if (level.ObservedIndex > index)
                {
                    position++;
                    continue;
                }

                if (level.Side == LiquiditySide.BuySide && candle.High > level.ZoneHigh)
                {
                    EmitLiquidityBreach(level, index);
                    unclassifiedLiquidityLevels.RemoveAt(position);
                    continue;
                }
                else if (level.Side == LiquiditySide.SellSide && candle.Low < level.ZoneLow)
                {
                    EmitLiquidityBreach(level, index);
                    unclassifiedLiquidityLevels.RemoveAt(position);
                    continue;
                }

                position++;
            }
        }

        private void EmitLiquidityBreach(LiquidityLevel level, int index)
        {
            level.Classified = true;
            var id = NextBreachId();
            var protectedSwing = level.Side == LiquiditySide.BuySide
                ? LastFinalizedSwing(SwingKind.Low)
                : LastFinalizedSwing(SwingKind.High);
            var eventType = level.Side == LiquiditySide.BuySide
                ? StructureEventType.BuySideLiquidityBreached
                : StructureEventType.SellSideLiquidityBreached;
            var direction = level.Side == LiquiditySide.BuySide
                ? StructureDirection.Bullish
                : StructureDirection.Bearish;
            var price = level.Side == LiquiditySide.BuySide ? level.ZoneHigh : level.ZoneLow;
            var breachEvent = Emit(
                eventType,
                observedIndex: index,
                subjectIndex: index,
                entityId: id,
                relatedEntityIds: [level.Id, .. level.RelatedSwingIds],
                direction: direction,
                price: price,
                zoneLow: level.ZoneLow,
                zoneHigh: level.ZoneHigh,
                breachedLiquidityId: level.Id,
                protectedSwingId: protectedSwing?.Id,
                classificationStage: "breached",
                sourceIndices: [index]);

            var breach = new LiquidityBreach(
                id,
                breachEvent.EventId,
                level,
                index,
                protectedSwing?.Id,
                protectedSwing?.Price);
            liquidityBreaches.Add(breach);
            openLiquidityBreaches.Add(breach);
        }

        private void UpdateOpenLiquidityBreaches(int index)
        {
            for (var position = 0; position < openLiquidityBreaches.Count;)
            {
                var breach = openLiquidityBreaches[position];
                if (breach.IsTerminal)
                {
                    openLiquidityBreaches.RemoveAt(position);
                    continue;
                }

                if (TryConfirmSweep(breach, index))
                {
                    openLiquidityBreaches.RemoveAt(position);
                    continue;
                }

                if (TryConfirmBreakout(breach, index))
                {
                    openLiquidityBreaches.RemoveAt(position);
                    continue;
                }

                if (TryConfirmSweepCandidate(breach, index))
                {
                    position++;
                    continue;
                }

                _ = TryConfirmRejection(breach, index);
                position++;
            }
        }

        private bool TryConfirmSweepCandidate(LiquidityBreach breach, int index)
        {
            if (breach.Stage != LiquidityBreachStage.Breached)
            {
                return false;
            }

            var candle = candles[index];
            var candidate = breach.Level.Side == LiquiditySide.BuySide
                ? candle.Close < breach.Level.ZoneHigh
                : candle.Close > breach.Level.ZoneLow;
            if (!candidate)
            {
                return false;
            }

            breach.Stage = LiquidityBreachStage.SweepCandidate;
            breach.CandidateIndex = index;
            Emit(
                breach.Level.Side == LiquiditySide.BuySide
                    ? StructureEventType.BuySideSweepCandidate
                    : StructureEventType.SellSideSweepCandidate,
                observedIndex: index,
                subjectIndex: index,
                entityId: breach.Id,
                relatedEntityIds: BuildBreachRelations(breach),
                direction: breach.Level.Side == LiquiditySide.BuySide
                    ? StructureDirection.Bearish
                    : StructureDirection.Bullish,
                price: breach.Level.Side == LiquiditySide.BuySide
                    ? breach.Level.ZoneHigh
                    : breach.Level.ZoneLow,
                zoneLow: breach.Level.ZoneLow,
                zoneHigh: breach.Level.ZoneHigh,
                breachedLiquidityId: breach.Level.Id,
                originalBreachEventId: breach.BreachEventId,
                protectedSwingId: breach.ProtectedSwingId,
                classificationStage: "sweepCandidate",
                sourceIndices: [breach.ObservedIndex, index]);

            return true;
        }

        private bool TryConfirmRejection(LiquidityBreach breach, int index)
        {
            if (breach.Stage != LiquidityBreachStage.SweepCandidate
                || breach.CandidateIndex is null
                || index <= breach.CandidateIndex)
            {
                return false;
            }

            if (!IsOppositeDisplacement(index, breach.Level.Side)
                && !HasOppositeFvgEndingAt(index, breach.Level.Side))
            {
                return false;
            }

            breach.Stage = LiquidityBreachStage.RejectionConfirmed;
            Emit(
                breach.Level.Side == LiquiditySide.BuySide
                    ? StructureEventType.BuySideRejectionConfirmed
                    : StructureEventType.SellSideRejectionConfirmed,
                observedIndex: index,
                subjectIndex: index,
                entityId: breach.Id,
                relatedEntityIds: BuildBreachRelations(breach),
                direction: breach.Level.Side == LiquiditySide.BuySide
                    ? StructureDirection.Bearish
                    : StructureDirection.Bullish,
                price: candles[index].Close,
                zoneLow: breach.Level.ZoneLow,
                zoneHigh: breach.Level.ZoneHigh,
                breachedLiquidityId: breach.Level.Id,
                originalBreachEventId: breach.BreachEventId,
                protectedSwingId: breach.ProtectedSwingId,
                classificationStage: "rejectionConfirmed",
                sourceIndices: [breach.ObservedIndex, index]);

            return true;
        }

        private bool TryConfirmSweep(LiquidityBreach breach, int index)
        {
            if (breach.Stage is not (LiquidityBreachStage.SweepCandidate or LiquidityBreachStage.RejectionConfirmed)
                || breach.ProtectedSwingPrice is null)
            {
                return false;
            }

            var candle = candles[index];
            var protectedBreak = breach.Level.Side == LiquiditySide.BuySide
                ? candle.Low < breach.ProtectedSwingPrice.Value
                : candle.High > breach.ProtectedSwingPrice.Value;
            if (!protectedBreak)
            {
                return false;
            }

            breach.Stage = LiquidityBreachStage.SweepConfirmed;
            Emit(
                breach.Level.Side == LiquiditySide.BuySide
                    ? StructureEventType.BuySideSweepConfirmed
                    : StructureEventType.SellSideSweepConfirmed,
                observedIndex: index,
                subjectIndex: index,
                entityId: breach.Id,
                relatedEntityIds: BuildBreachRelations(breach),
                direction: breach.Level.Side == LiquiditySide.BuySide
                    ? StructureDirection.Bearish
                    : StructureDirection.Bullish,
                price: breach.ProtectedSwingPrice,
                zoneLow: breach.Level.ZoneLow,
                zoneHigh: breach.Level.ZoneHigh,
                breachedLiquidityId: breach.Level.Id,
                originalBreachEventId: breach.BreachEventId,
                protectedSwingId: breach.ProtectedSwingId,
                classificationStage: "sweepConfirmed",
                sourceIndices: [breach.ObservedIndex, index]);

            return true;
        }

        private bool TryConfirmBreakout(LiquidityBreach breach, int index)
        {
            if (breach.Stage == LiquidityBreachStage.RejectionConfirmed)
            {
                return false;
            }

            var candle = candles[index];
            var acceptedBeyondLevel = breach.Level.Side == LiquiditySide.BuySide
                ? candle.Close > breach.Level.ZoneHigh
                : candle.Close < breach.Level.ZoneLow;
            if (!acceptedBeyondLevel)
            {
                return false;
            }

            breach.Stage = LiquidityBreachStage.BreakoutConfirmed;
            Emit(
                breach.Level.Side == LiquiditySide.BuySide
                    ? StructureEventType.BuySideBreakoutConfirmed
                    : StructureEventType.SellSideBreakoutConfirmed,
                observedIndex: index,
                subjectIndex: index,
                entityId: breach.Id,
                relatedEntityIds: BuildBreachRelations(breach),
                direction: breach.Level.Side == LiquiditySide.BuySide
                    ? StructureDirection.Bullish
                    : StructureDirection.Bearish,
                price: candle.Close,
                zoneLow: breach.Level.ZoneLow,
                zoneHigh: breach.Level.ZoneHigh,
                breachedLiquidityId: breach.Level.Id,
                originalBreachEventId: breach.BreachEventId,
                protectedSwingId: breach.ProtectedSwingId,
                classificationStage: "breakoutConfirmed",
                sourceIndices: [breach.ObservedIndex, index]);

            return true;
        }

        private bool IsOppositeDisplacement(int index, LiquiditySide side)
        {
            if (index < settings.ExpansionLookbackCandles)
            {
                return false;
            }

            var candle = candles[index];
            var bearish = candle.Close < candle.Open;
            var bullish = candle.Close > candle.Open;
            if ((side == LiquiditySide.BuySide && !bearish)
                || (side == LiquiditySide.SellSide && !bullish))
            {
                return false;
            }

            if (!TryGetAveragePriorRange(index, out var averageRange))
            {
                return false;
            }

            var body = Math.Abs(candle.Close - candle.Open);
            return body >= averageRange * settings.ExpansionBodyToAverageRange;
        }

        private bool HasOppositeFvgEndingAt(int observedIndex, LiquiditySide side)
        {
            if (observedIndex < 2)
            {
                return false;
            }

            var middleIndex = observedIndex - 1;
            if (!IsOppositeDisplacement(middleIndex, side))
            {
                return false;
            }

            var first = candles[middleIndex - 1];
            var third = candles[middleIndex + 1];
            return side == LiquiditySide.BuySide
                ? first.Low > third.High
                : first.High < third.Low;
        }

        private string[] BuildBreachRelations(LiquidityBreach breach)
        {
            return breach.ProtectedSwingId is null
                ? [breach.Level.Id, .. breach.Level.RelatedSwingIds]
                : [breach.Level.Id, breach.ProtectedSwingId, .. breach.Level.RelatedSwingIds];
        }

        private SwingPoint? LastFinalizedSwing(SwingKind kind)
        {
            for (var index = finalizedSwings.Count - 1; index >= 0; index--)
            {
                var swing = finalizedSwings[index];
                if (swing.Kind == kind)
                {
                    return swing;
                }
            }

            return null;
        }

        private SwingPoint? LastFinalizedSwing()
        {
            return finalizedSwings.LastOrDefault();
        }

        private void TryDiscoverOrderBlock(int index, StructureDirection direction)
        {
            var candidateIndex = FindOrderBlockCandidate(index, direction);
            if (candidateIndex is null)
            {
                return;
            }

            var key = FormattableString.Invariant($"{direction}:{candidateIndex.Value}");
            if (!discoveredOrderBlocks.Add(key))
            {
                return;
            }

            var candidate = candles[candidateIndex.Value];
            var id = NextOrderBlockId();
            var eventType = direction == StructureDirection.Bullish
                ? StructureEventType.BullishOrderBlockDiscovered
                : StructureEventType.BearishOrderBlockDiscovered;
            var orderBlock = new OrderBlockInfo(
                id,
                direction,
                candidateIndex.Value,
                index,
                candidate.Low,
                candidate.High);

            orderBlocks.Add(orderBlock);
            activeOrderBlocks.Add(orderBlock);
            Emit(
                eventType,
                observedIndex: index,
                subjectIndex: candidateIndex.Value,
                entityId: id,
                direction: direction,
                zoneLow: candidate.Low,
                zoneHigh: candidate.High,
                orderBlockState: OrderBlockState.Active,
                sourceIndices: [candidateIndex.Value, index]);
        }

        private int? FindOrderBlockCandidate(int index, StructureDirection direction)
        {
            if (index == 0)
            {
                return null;
            }

            var displacement = candles[index];
            var lowerBound = Math.Max(0, index - settings.OrderBlockSearchBackCandles);
            for (var candidateIndex = index - 1; candidateIndex >= lowerBound; candidateIndex--)
            {
                var candidate = candles[candidateIndex];
                if (direction == StructureDirection.Bullish
                    && candidate.Close < candidate.Open
                    && displacement.Close > candidate.High)
                {
                    return candidateIndex;
                }

                if (direction == StructureDirection.Bearish
                    && candidate.Close > candidate.Open
                    && displacement.Close < candidate.Low)
                {
                    return candidateIndex;
                }
            }

            return null;
        }

        private void TryDetectExpansionFvg(
            int observedIndex,
            int middleIndex,
            StructureDirection direction)
        {
            var orderBlock = FindOrderBlockForExpansion(middleIndex, direction);
            if (orderBlock is null || !IsExpansionCandle(middleIndex))
            {
                return;
            }

            if (!TryGetFvgZone(middleIndex, direction, out var zoneLow, out var zoneHigh))
            {
                return;
            }

            var key = FormattableString.Invariant($"{direction}:{orderBlock.Id}:{middleIndex}");
            if (!discoveredFvgs.Add(key))
            {
                return;
            }

            var expansionType = direction == StructureDirection.Bullish
                ? StructureEventType.BullishExpansion
                : StructureEventType.BearishExpansion;
            var fvgType = direction == StructureDirection.Bullish
                ? StructureEventType.BullishFvgDiscovered
                : StructureEventType.BearishFvgDiscovered;

            Emit(
                expansionType,
                observedIndex,
                middleIndex,
                relatedEntityIds: [orderBlock.Id],
                direction: direction,
                price: candles[middleIndex].Close,
                sourceIndices: [middleIndex - 1, middleIndex, middleIndex + 1]);

            var fvgId = NextFvgId();
            Emit(
                fvgType,
                observedIndex,
                middleIndex,
                entityId: fvgId,
                relatedEntityIds: [orderBlock.Id],
                direction: direction,
                zoneLow: zoneLow,
                zoneHigh: zoneHigh,
                fillPercent: 0m,
                fvgState: FvgState.Active,
                sourceIndices: [middleIndex - 1, middleIndex, middleIndex + 1]);

            var fvg = new FvgInfo(
                fvgId,
                orderBlock.Id,
                direction,
                middleIndex,
                observedIndex,
                zoneLow,
                zoneHigh);
            fvgs.Add(fvg);
            activeFvgs.Add(fvg);
        }

        private OrderBlockInfo? FindOrderBlockForExpansion(int middleIndex, StructureDirection direction)
        {
            OrderBlockInfo? bestMatch = null;
            var candle = candles[middleIndex];
            foreach (var orderBlock in activeOrderBlocks)
            {
                if (orderBlock.Direction != direction
                    || middleIndex <= orderBlock.SubjectIndex
                    || middleIndex - orderBlock.SubjectIndex > settings.ExpansionFvgWindowCandles
                    || !ClosesBeyondOrderBlock(candle, orderBlock))
                {
                    continue;
                }

                if (bestMatch is null || orderBlock.SubjectIndex > bestMatch.SubjectIndex)
                {
                    bestMatch = orderBlock;
                }
            }

            return bestMatch;
        }

        private static bool ClosesBeyondOrderBlock(CandleEvent candle, OrderBlockInfo orderBlock)
        {
            return orderBlock.Direction == StructureDirection.Bullish
                ? candle.Close > orderBlock.ZoneHigh
                : candle.Close < orderBlock.ZoneLow;
        }

        private bool IsExpansionCandle(int middleIndex)
        {
            if (!TryGetAveragePriorRange(middleIndex, out var averageRange))
            {
                return false;
            }

            var body = Math.Abs(candles[middleIndex].Close - candles[middleIndex].Open);
            return body >= averageRange * settings.ExpansionBodyToAverageRange;
        }

        private bool TryGetAveragePriorRange(int index, out decimal averageRange)
        {
            averageRange = 0m;
            var lookback = settings.ExpansionLookbackCandles;
            if (index < lookback)
            {
                return false;
            }

            var startIndex = index - lookback;
            var rangeSum = candleRangePrefixSums[index] - candleRangePrefixSums[startIndex];
            averageRange = rangeSum / lookback;
            return averageRange > 0m;
        }

        private SwingPoint? PreviousFinalizedSwing(SwingPoint current)
        {
            for (var index = finalizedSwings.Count - 1; index >= 0; index--)
            {
                var swing = finalizedSwings[index];
                if (swing.Kind == current.Kind && swing.Id != current.Id)
                {
                    return swing;
                }
            }

            return null;
        }

        private static decimal[] BuildCandleRangePrefixSums(IReadOnlyList<CandleEvent> candles)
        {
            var prefixSums = new decimal[candles.Count + 1];
            for (var index = 0; index < candles.Count; index++)
            {
                prefixSums[index + 1] = prefixSums[index] + candles[index].High - candles[index].Low;
            }

            return prefixSums;
        }

        private bool TryGetFvgZone(
            int middleIndex,
            StructureDirection direction,
            out decimal zoneLow,
            out decimal zoneHigh)
        {
            var first = candles[middleIndex - 1];
            var third = candles[middleIndex + 1];

            if (direction == StructureDirection.Bullish && first.High < third.Low)
            {
                zoneLow = first.High;
                zoneHigh = third.Low;
                return true;
            }

            if (direction == StructureDirection.Bearish && first.Low > third.High)
            {
                zoneLow = third.High;
                zoneHigh = first.Low;
                return true;
            }

            zoneLow = 0m;
            zoneHigh = 0m;
            return false;
        }

        private decimal CalculateFillPercent(FvgInfo fvg, CandleEvent candle)
        {
            var zoneSize = fvg.ZoneHigh - fvg.ZoneLow;
            if (zoneSize <= 0m)
            {
                return fvg.FillPercent;
            }

            decimal penetration;
            if (fvg.Direction == StructureDirection.Bullish)
            {
                if (candle.Low >= fvg.ZoneHigh)
                {
                    return fvg.FillPercent;
                }

                penetration = fvg.ZoneHigh - Math.Max(candle.Low, fvg.ZoneLow);
            }
            else
            {
                if (candle.High <= fvg.ZoneLow)
                {
                    return fvg.FillPercent;
                }

                penetration = Math.Min(candle.High, fvg.ZoneHigh) - fvg.ZoneLow;
            }

            return Math.Clamp(penetration / zoneSize * 100m, 0m, 100m);
        }

        private static decimal CalculateDistanceBasisPoints(decimal first, decimal second)
        {
            var denominator = (first + second) / 2m;
            if (denominator <= 0m)
            {
                return decimal.MaxValue;
            }

            return Math.Abs(first - second) / denominator * 10_000m;
        }

        private static bool IntersectsZone(CandleEvent candle, decimal zoneLow, decimal zoneHigh)
        {
            return candle.High >= zoneLow && candle.Low <= zoneHigh;
        }

        private StructureEvent Emit(
            StructureEventType eventType,
            int observedIndex,
            int? subjectIndex = null,
            string? entityId = null,
            string[]? relatedEntityIds = null,
            StructureDirection? direction = null,
            decimal? price = null,
            decimal? zoneLow = null,
            decimal? zoneHigh = null,
            decimal? fillPercent = null,
            decimal? distanceBasisPoints = null,
            OrderBlockState? orderBlockState = null,
            FvgState? fvgState = null,
            string? breachedLiquidityId = null,
            string? originalBreachEventId = null,
            string? protectedSwingId = null,
            string? classificationStage = null,
            int[]? sourceIndices = null)
        {
            var sequence = ++eventSequence;
            var structureEvent = new StructureEvent(
                identity.RunId,
                sequence,
                FormatId("structure", sequence),
                eventType,
                candles[observedIndex].OpenTimeUtc,
                subjectIndex is null ? null : candles[subjectIndex.Value].OpenTimeUtc,
                identity.MarketId,
                identity.ExchangeId,
                identity.Timeframe,
                entityId,
                relatedEntityIds,
                direction,
                price,
                zoneLow,
                zoneHigh,
                fillPercent,
                distanceBasisPoints,
                orderBlockState,
                fvgState,
                breachedLiquidityId,
                originalBreachEventId,
                protectedSwingId,
                classificationStage,
                sourceIndices?.Select(index => candles[index].OpenTimeUtc).ToArray());

            events.Add(structureEvent);
            return structureEvent;
        }

        private string NextSwingId()
        {
            return FormatId("swing", ++swingSequence);
        }

        private string NextLiquidityId()
        {
            return FormatId("liq", ++liquiditySequence);
        }

        private string NextBreachId()
        {
            return FormatId("breach", ++breachSequence);
        }

        private string NextOrderBlockId()
        {
            return FormatId("ob", ++orderBlockSequence);
        }

        private string NextFvgId()
        {
            return FormatId("fvg", ++fvgSequence);
        }

        private static string FormatId(string prefix, int sequence)
        {
            return FormattableString.Invariant($"{prefix}-{sequence:000000}");
        }
    }
}
