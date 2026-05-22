namespace Wickd.Models;

/// <summary>
/// Represents a validated command request resolved against Wickd configuration.
/// </summary>
/// <param name="Market">Resolved market definition.</param>
/// <param name="Timeframe">Validated candle timeframe.</param>
/// <param name="DateRange">Validated UTC candle date range.</param>
/// <param name="CandleCachePath">Derived JSONL candle cache path for the request.</param>
public sealed record RunRequest(
    MarketDefinition Market,
    Timeframe Timeframe,
    DateRange DateRange,
    string CandleCachePath);
