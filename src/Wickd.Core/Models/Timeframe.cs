#nullable enable

namespace Wickd.Models;

/// <summary>
/// Represents a supported candle timeframe and its exact duration.
/// </summary>
public readonly record struct Timeframe
{
    private static readonly IReadOnlyDictionary<string, TimeSpan> SupportedTimeframes =
        new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
        {
            ["1m"] = TimeSpan.FromMinutes(1),
            ["5m"] = TimeSpan.FromMinutes(5),
            ["15m"] = TimeSpan.FromMinutes(15),
            ["1h"] = TimeSpan.FromHours(1),
            ["4h"] = TimeSpan.FromHours(4),
            ["1d"] = TimeSpan.FromDays(1)
        };

    private Timeframe(string value, TimeSpan duration)
    {
        Value = value;
        Duration = duration;
    }

    /// <summary>
    /// Gets the canonical timeframe value used in CLI arguments, cache paths, and journals.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the exact wall-clock duration represented by this timeframe.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the supported timeframe strings for MVP candle processing.
    /// </summary>
    public static IReadOnlyCollection<string> SupportedValues => SupportedTimeframes.Keys.ToArray();

    /// <summary>
    /// Parses a supported timeframe string.
    /// </summary>
    /// <param name="value">The timeframe string to parse.</param>
    /// <returns>The parsed timeframe.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not supported.</exception>
    public static Timeframe Parse(string value)
    {
        if (TryParse(value, out var timeframe))
        {
            return timeframe;
        }

        throw new ArgumentException(
            $"Unsupported timeframe '{value}'. Supported values: {string.Join(", ", SupportedTimeframes.Keys)}.",
            nameof(value));
    }

    /// <summary>
    /// Attempts to parse a supported timeframe string.
    /// </summary>
    /// <param name="value">The timeframe string to parse.</param>
    /// <param name="timeframe">The parsed timeframe when parsing succeeds.</param>
    /// <returns><see langword="true" /> when the value is supported; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(string? value, out Timeframe timeframe)
    {
        if (value is not null && SupportedTimeframes.TryGetValue(value, out var duration))
        {
            timeframe = new Timeframe(value, duration);
            return true;
        }

        timeframe = default;
        return false;
    }

    /// <summary>
    /// Returns the canonical timeframe string.
    /// </summary>
    /// <returns>The canonical timeframe string.</returns>
    public override string ToString()
    {
        return Value;
    }
}
