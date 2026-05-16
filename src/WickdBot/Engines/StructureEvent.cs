#nullable enable

using System.Text.Json.Serialization;

namespace WickdBot.Engines;

/// <summary>
/// Represents one deterministic market-structure journal event.
/// </summary>
internal sealed record StructureEvent
{
    /// <summary>
    /// Initializes a structure event.
    /// </summary>
    /// <param name="runId">Backtest run ID.</param>
    /// <param name="sequence">One-based event sequence within the run.</param>
    /// <param name="eventId">Deterministic event ID.</param>
    /// <param name="eventType">Structure event type.</param>
    /// <param name="observedOpenTimeUtc">UTC open time of the candle that made the event knowable.</param>
    /// <param name="subjectOpenTimeUtc">UTC open time of the candle primarily described by the event.</param>
    /// <param name="marketId">Canonical WickdBot market ID.</param>
    /// <param name="exchangeId">Exchange identifier that produced the candles.</param>
    /// <param name="timeframe">Canonical candle timeframe.</param>
    /// <param name="entityId">Structure entity ID affected by this event.</param>
    /// <param name="relatedEntityIds">Related structure entity IDs.</param>
    /// <param name="direction">Directional side when the event has one.</param>
    /// <param name="price">Primary price associated with the event.</param>
    /// <param name="zoneLow">Lower bound of a journaled structure zone.</param>
    /// <param name="zoneHigh">Upper bound of a journaled structure zone.</param>
    /// <param name="fillPercent">Fair-value gap fill percentage when applicable.</param>
    /// <param name="distanceBasisPoints">Distance between equal levels when applicable.</param>
    /// <param name="orderBlockState">Order-block lifecycle state when applicable.</param>
    /// <param name="fvgState">Fair-value gap lifecycle state when applicable.</param>
    /// <param name="breachedLiquidityId">Liquidity entity ID taken by a staged breach event.</param>
    /// <param name="originalBreachEventId">Original breach event ID for later sweep/breakout lifecycle events.</param>
    /// <param name="protectedSwingId">Intervening swing ID used to confirm or reject the breach.</param>
    /// <param name="classificationStage">Human-readable lifecycle stage for staged structure events.</param>
    /// <param name="sourceOpenTimesUtc">UTC candle open times used as source evidence.</param>
    /// <exception cref="ArgumentException">Thrown when a supplied timestamp is not UTC.</exception>
    [JsonConstructor]
    public StructureEvent(
        string runId,
        int sequence,
        string eventId,
        StructureEventType eventType,
        DateTimeOffset observedOpenTimeUtc,
        DateTimeOffset? subjectOpenTimeUtc,
        string marketId,
        string exchangeId,
        string timeframe,
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
        DateTimeOffset[]? sourceOpenTimesUtc = null)
    {
        EnsureUtc(observedOpenTimeUtc, nameof(observedOpenTimeUtc));
        if (subjectOpenTimeUtc is { } subject)
        {
            EnsureUtc(subject, nameof(subjectOpenTimeUtc));
        }

        foreach (var sourceOpenTimeUtc in sourceOpenTimesUtc ?? [])
        {
            EnsureUtc(sourceOpenTimeUtc, nameof(sourceOpenTimesUtc));
        }

        RunId = runId;
        Sequence = sequence;
        EventId = eventId;
        EventType = eventType;
        ObservedOpenTimeUtc = observedOpenTimeUtc;
        SubjectOpenTimeUtc = subjectOpenTimeUtc;
        MarketId = marketId;
        ExchangeId = exchangeId;
        Timeframe = timeframe;
        EntityId = entityId;
        RelatedEntityIds = relatedEntityIds ?? [];
        Direction = direction;
        Price = price;
        ZoneLow = zoneLow;
        ZoneHigh = zoneHigh;
        FillPercent = fillPercent;
        DistanceBasisPoints = distanceBasisPoints;
        OrderBlockState = orderBlockState;
        FvgState = fvgState;
        BreachedLiquidityId = breachedLiquidityId;
        OriginalBreachEventId = originalBreachEventId;
        ProtectedSwingId = protectedSwingId;
        ClassificationStage = classificationStage;
        SourceOpenTimesUtc = sourceOpenTimesUtc ?? [];
    }

    /// <summary>
    /// Gets the backtest run ID.
    /// </summary>
    public string RunId { get; }

    /// <summary>
    /// Gets the one-based event sequence within the run.
    /// </summary>
    public int Sequence { get; }

    /// <summary>
    /// Gets the deterministic event ID.
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// Gets the structure event type.
    /// </summary>
    public StructureEventType EventType { get; }

    /// <summary>
    /// Gets the UTC open time of the candle that made this event knowable.
    /// </summary>
    public DateTimeOffset ObservedOpenTimeUtc { get; }

    /// <summary>
    /// Gets the UTC open time of the candle primarily described by this event.
    /// </summary>
    public DateTimeOffset? SubjectOpenTimeUtc { get; }

    /// <summary>
    /// Gets the canonical WickdBot market ID.
    /// </summary>
    public string MarketId { get; }

    /// <summary>
    /// Gets the exchange identifier that produced the candles.
    /// </summary>
    public string ExchangeId { get; }

    /// <summary>
    /// Gets the canonical candle timeframe.
    /// </summary>
    public string Timeframe { get; }

    /// <summary>
    /// Gets the structure entity ID affected by this event.
    /// </summary>
    public string? EntityId { get; }

    /// <summary>
    /// Gets related structure entity IDs.
    /// </summary>
    public string[] RelatedEntityIds { get; }

    /// <summary>
    /// Gets the directional side when the event has one.
    /// </summary>
    public StructureDirection? Direction { get; }

    /// <summary>
    /// Gets the primary price associated with the event.
    /// </summary>
    public decimal? Price { get; }

    /// <summary>
    /// Gets the lower bound of a journaled structure zone.
    /// </summary>
    public decimal? ZoneLow { get; }

    /// <summary>
    /// Gets the upper bound of a journaled structure zone.
    /// </summary>
    public decimal? ZoneHigh { get; }

    /// <summary>
    /// Gets fair-value gap fill percentage when applicable.
    /// </summary>
    public decimal? FillPercent { get; }

    /// <summary>
    /// Gets equal-level distance in basis points when applicable.
    /// </summary>
    public decimal? DistanceBasisPoints { get; }

    /// <summary>
    /// Gets order-block lifecycle state when applicable.
    /// </summary>
    public OrderBlockState? OrderBlockState { get; }

    /// <summary>
    /// Gets fair-value gap lifecycle state when applicable.
    /// </summary>
    public FvgState? FvgState { get; }

    /// <summary>
    /// Gets the liquidity entity ID taken by a staged breach event.
    /// </summary>
    public string? BreachedLiquidityId { get; }

    /// <summary>
    /// Gets the original breach event ID for later sweep or breakout lifecycle events.
    /// </summary>
    public string? OriginalBreachEventId { get; }

    /// <summary>
    /// Gets the protected or intervening swing ID used to classify the breach.
    /// </summary>
    public string? ProtectedSwingId { get; }

    /// <summary>
    /// Gets the human-readable lifecycle stage for staged structure events.
    /// </summary>
    public string? ClassificationStage { get; }

    /// <summary>
    /// Gets UTC candle open times used as source evidence.
    /// </summary>
    public DateTimeOffset[] SourceOpenTimesUtc { get; }

    /// <summary>
    /// Validates that a timestamp is expressed as UTC.
    /// </summary>
    /// <param name="value">Timestamp to validate.</param>
    /// <param name="parameterName">Parameter name used in the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the timestamp is not UTC.</exception>
    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Structure event timestamps must be expressed in UTC.", parameterName);
        }
    }
}
