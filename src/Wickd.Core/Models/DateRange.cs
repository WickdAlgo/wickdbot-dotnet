namespace Wickd.Models;

/// <summary>
/// Represents an exclusive UTC time window for fetching or replaying candles.
/// </summary>
public sealed record DateRange
{
    /// <summary>
    /// Initializes a new UTC date range.
    /// </summary>
    /// <param name="fromUtc">Inclusive UTC start time.</param>
    /// <param name="toUtc">Exclusive UTC end time.</param>
    /// <exception cref="ArgumentException">Thrown when either endpoint is not UTC or the range is empty.</exception>
    public DateRange(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        if (fromUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The start time must be expressed in UTC.", nameof(fromUtc));
        }

        if (toUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The end time must be expressed in UTC.", nameof(toUtc));
        }

        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("The end time must be later than the start time.", nameof(toUtc));
        }

        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    /// <summary>
    /// Gets the inclusive UTC start time.
    /// </summary>
    public DateTimeOffset FromUtc { get; }

    /// <summary>
    /// Gets the exclusive UTC end time.
    /// </summary>
    public DateTimeOffset ToUtc { get; }
}
