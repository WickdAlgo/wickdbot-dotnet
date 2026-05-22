using Wickd.Models;

namespace Wickd.Tests;

/// <summary>
/// Verifies supported MVP timeframe parsing behavior.
/// </summary>
public class TimeframeTests
{
    /// <summary>
    /// Confirms each supported timeframe parses to the expected duration.
    /// </summary>
    /// <param name="value">The timeframe string.</param>
    /// <param name="expectedDuration">The expected duration.</param>
    [Theory]
    [InlineData("1m", "00:01:00")]
    [InlineData("5m", "00:05:00")]
    [InlineData("15m", "00:15:00")]
    [InlineData("1h", "01:00:00")]
    [InlineData("4h", "04:00:00")]
    [InlineData("1d", "1.00:00:00")]
    public void ParseAcceptsSupportedTimeframes(string value, string expectedDuration)
    {
        var timeframe = Timeframe.Parse(value);

        Assert.Equal(value, timeframe.Value);
        Assert.Equal(TimeSpan.Parse(expectedDuration), timeframe.Duration);
    }

    /// <summary>
    /// Confirms unsupported timeframe strings are rejected.
    /// </summary>
    [Fact]
    public void ParseRejectsUnsupportedTimeframe()
    {
        var exception = Assert.Throws<ArgumentException>(() => Timeframe.Parse("2m"));

        Assert.Contains("Unsupported timeframe '2m'", exception.Message);
    }
}
