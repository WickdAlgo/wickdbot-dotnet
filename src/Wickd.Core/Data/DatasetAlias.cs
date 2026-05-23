#nullable enable

using System.Text.Json.Serialization;

namespace Wickd.Data;

/// <summary>
/// Describes a friendly name for a deterministic historical candle cache range.
/// </summary>
internal sealed record DatasetAlias
{
    /// <summary>
    /// Initializes a dataset alias record.
    /// </summary>
    /// <param name="alias">Friendly dataset alias.</param>
    /// <param name="marketId">Canonical Wickd market ID.</param>
    /// <param name="exchangeId">Exchange identifier.</param>
    /// <param name="timeframe">Canonical timeframe string.</param>
    /// <param name="fromUtc">Inclusive UTC start time.</param>
    /// <param name="toUtc">Exclusive UTC end time.</param>
    /// <param name="candleCachePath">Deterministic candle cache path.</param>
    /// <param name="createdAtUtc">UTC timestamp when the alias was created.</param>
    /// <param name="updatedAtUtc">UTC timestamp when the alias was last updated.</param>
    /// <exception cref="DatasetAliasException">Thrown when alias metadata is invalid.</exception>
    [JsonConstructor]
    public DatasetAlias(
        string alias,
        string marketId,
        string exchangeId,
        string timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string candleCachePath,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        ValidateAlias(alias);
        ValidateRequired(marketId, "Market ID");
        ValidateRequired(exchangeId, "Exchange ID");
        ValidateRequired(timeframe, "Timeframe");
        ValidateRequired(candleCachePath, "Candle cache path");
        ValidateUtc(fromUtc, nameof(fromUtc));
        ValidateUtc(toUtc, nameof(toUtc));
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        ValidateUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (toUtc <= fromUtc)
        {
            throw new DatasetAliasException("Dataset alias end time must be later than start time.");
        }

        Alias = alias;
        MarketId = marketId;
        ExchangeId = exchangeId;
        Timeframe = timeframe;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        CandleCachePath = candleCachePath;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Gets the friendly dataset alias.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the canonical Wickd market ID.
    /// </summary>
    public string MarketId { get; }

    /// <summary>
    /// Gets the exchange identifier.
    /// </summary>
    public string ExchangeId { get; }

    /// <summary>
    /// Gets the canonical timeframe string.
    /// </summary>
    public string Timeframe { get; }

    /// <summary>
    /// Gets the inclusive UTC start time.
    /// </summary>
    public DateTimeOffset FromUtc { get; }

    /// <summary>
    /// Gets the exclusive UTC end time.
    /// </summary>
    public DateTimeOffset ToUtc { get; }

    /// <summary>
    /// Gets the deterministic candle cache path.
    /// </summary>
    public string CandleCachePath { get; }

    /// <summary>
    /// Gets the UTC timestamp when the alias was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when the alias was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    /// <summary>
    /// Validates a dataset alias name.
    /// </summary>
    /// <param name="alias">Alias name to validate.</param>
    /// <exception cref="DatasetAliasException">Thrown when the alias is empty or contains unsupported characters.</exception>
    internal static void ValidateAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DatasetAliasException("Dataset alias is required.");
        }

        foreach (var character in alias)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '.'
                && character is not '_'
                && character is not '-')
            {
                throw new DatasetAliasException(
                    "Dataset alias may contain only letters, numbers, '.', '_', and '-'.");
            }
        }
    }

    /// <summary>
    /// Validates a required string metadata field.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="fieldName">Field name used in diagnostics.</param>
    /// <exception cref="DatasetAliasException">Thrown when the value is empty.</exception>
    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DatasetAliasException($"{fieldName} is required.");
        }
    }

    /// <summary>
    /// Validates that a timestamp is expressed in UTC.
    /// </summary>
    /// <param name="timestamp">Timestamp to validate.</param>
    /// <param name="parameterName">Parameter name used in diagnostics.</param>
    /// <exception cref="DatasetAliasException">Thrown when the timestamp is not UTC.</exception>
    private static void ValidateUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
        {
            throw new DatasetAliasException($"{parameterName} must be expressed in UTC.");
        }
    }
}
