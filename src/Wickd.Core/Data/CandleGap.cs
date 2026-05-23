namespace Wickd.Data;

/// <summary>
/// Describes a missing candle interval detected during normalization.
/// </summary>
/// <param name="ExpectedOpenTimeUtc">The next expected UTC candle open time.</param>
/// <param name="ActualOpenTimeUtc">The actual UTC candle open time that followed the gap.</param>
/// <param name="MissingCandles">Estimated number of missing candle slots.</param>
public sealed record CandleGap(
    DateTimeOffset ExpectedOpenTimeUtc,
    DateTimeOffset ActualOpenTimeUtc,
    int MissingCandles);
