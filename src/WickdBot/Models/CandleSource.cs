namespace WickdBot.Models;

/// <summary>
/// Identifies where a candle came from in the WickdBot pipeline.
/// </summary>
internal enum CandleSource
{
    /// <summary>
    /// Historical exchange data before replay.
    /// </summary>
    Historical,

    /// <summary>
    /// Historical data replayed through a backtest run.
    /// </summary>
    Backtest,

    /// <summary>
    /// Future live market data.
    /// </summary>
    Live
}
