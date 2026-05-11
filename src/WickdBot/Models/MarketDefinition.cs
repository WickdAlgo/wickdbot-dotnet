namespace WickdBot.Models;

/// <summary>
/// Maps a canonical WickdBot market ID to an exchange-specific market identity.
/// </summary>
/// <param name="MarketId">Canonical WickdBot market ID.</param>
/// <param name="ExchangeId">Exchange identifier used by market data adapters.</param>
/// <param name="ExchangeSymbol">Exchange-specific symbol used when requesting candles.</param>
internal sealed record MarketDefinition(string MarketId, string ExchangeId, string ExchangeSymbol);
