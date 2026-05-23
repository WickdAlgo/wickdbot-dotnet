---
title: Programmatic Business Flows
---

# Programmatic Business Flows

These diagrams show the currently implemented Wickd program paths. Phase 2 implements historical fetch, deterministic candle caching, dataset aliases, and cached candle replay. Strategy execution and analysis are still placeholders.

## CLI Command Flow

```mermaid
flowchart TD
    A["Program.Main(args)"] --> B["BuildRootCommand()"]
    B --> C["Create HistoricalDataSource"]
    C --> D{"Subcommand"}
    D --> E["fetch"]
    D --> F["backtest"]
    D --> G["analyze"]

    E --> H["Load settings"]
    H --> I["Resolve explicit run request"]
    I --> J["HistoricalDataSource.LoadOrFetchAsync"]
    J --> K{"--alias supplied?"}
    K -->|Yes| L["DatasetAliasCatalog.SaveAsync"]
    K -->|No| M["Write fetch result"]
    L --> M
    M --> N["Exit 0"]

    F --> O["Load settings"]
    O --> P{"--dataset supplied?"}
    P -->|Yes| Q["Resolve alias to RunRequest"]
    P -->|No| R["Resolve explicit run request"]
    Q --> S["Use supplied or generated run ID"]
    R --> S
    S --> T["HistoricalDataSource.ReplayAsync"]
    T --> U["Write replay result"]
    U --> N

    G --> V["Read required --run-id"]
    V --> W["ReturnNotImplemented"]
    W --> X["Exit 1"]

    H --> Y{"Validation or data failure?"}
    I --> Y
    J --> Y
    L --> Y
    O --> Y
    Q --> Y
    R --> Y
    T --> Y
    Y -->|Yes| Z["Write error and exit 2"]
```

## Historical Fetch And Cache Flow

```mermaid
flowchart TD
    A["LoadOrFetchAsync(request)"] --> B["Validate completed candle range"]
    B --> C{"Cache file exists?"}

    C -->|Yes| D["CandleJsonLines.ReadAsync"]
    D --> E["Validate cached candle identity"]
    E --> F["Normalize historical candles"]
    F --> G["Ensure at least one candle"]
    G --> H["HistoricalDataResult cache hit"]

    C -->|No| I["Resolve exchange client"]
    I --> J{"Exchange supported?"}
    J -->|No| K["WickdDataException"]
    J -->|Yes| L["IMarketDataClient.FetchCandlesAsync"]
    L --> M["Filter candles to request range"]
    M --> N["Convert ExchangeCandle to Historical CandleEvent"]
    N --> F
    G --> O["CandleJsonLines.WriteAsync"]
    O --> P["HistoricalDataResult cache miss"]
```

## Binance CCXT Fetch Flow

```mermaid
flowchart TD
    A["CcxtBinanceMarketDataClient.FetchCandlesAsync"] --> B["Require configured exchange ID binance"]
    B --> C["Create CCXT binanceusdm adapter"]
    C --> D["Start at request FromUtc"]
    D --> E{"Before request ToUtc?"}
    E -->|No| F["Return fetched ExchangeCandles"]
    E -->|Yes| G["Count next fetch page limit"]
    G --> H["Fetch OHLCV page"]
    H --> I["Require non-null page"]
    I --> J{"Page empty?"}
    J -->|Yes| F
    J -->|No| K["Convert OHLCV to ExchangeCandle"]
    K --> L["Keep candles inside range"]
    L --> M{"Timestamps advanced?"}
    M -->|No| N["WickdDataException"]
    M -->|Yes| O["Advance since timestamp"]
    O --> P{"Short page?"}
    P -->|Yes| F
    P -->|No| E
```

## Dataset Alias Flow

```mermaid
flowchart TD
    A["fetch --alias name"] --> B["LoadOrFetchAsync result"]
    B --> C["DatasetAliasCatalog.CreateDefault"]
    C --> D["SaveAsync(alias, result, force)"]
    D --> E["Validate alias characters"]
    E --> F["Open exclusive catalog lock"]
    F --> G["Load existing data/datasets.json"]
    G --> H{"Alias exists and --force is false?"}
    H -->|Yes| I["DatasetAliasException"]
    H -->|No| J["Create or replace DatasetAlias"]
    J --> K["Atomic write catalog"]
    K --> L["Print saved alias"]

    M["backtest --dataset name"] --> N["Reject mixed explicit range options"]
    N --> O["ResolveRunRequestAsync"]
    O --> P["Load alias from catalog"]
    P --> Q["Resolve configured market"]
    Q --> R["Validate alias exchange still matches settings"]
    R --> S["Create RunRequest using alias cache path"]
```

## Backtest Replay Flow

```mermaid
flowchart TD
    A["ReplayAsync(request, runId)"] --> B["Require non-empty run ID"]
    B --> C["Validate completed candle range"]
    C --> D{"Cache file exists?"}
    D -->|No| E["Ask user to run fetch first"]
    D -->|Yes| F["CandleJsonLines.ReadAsync"]
    F --> G["Validate cached historical candle identity"]
    G --> H["Normalize cached candles"]
    H --> I["Ensure at least one candle"]
    I --> J["Convert Historical source to Backtest source"]
    J --> K["Attach run ID to each replayed candle"]
    K --> L["CandleReplayResult"]
    L --> M["Write replay summary"]
    M --> N["Stop before strategy execution"]
```

## Normalization And JSONL Flow

```mermaid
flowchart TD
    A["Input candle events"] --> B["Sort by OpenTimeUtc"]
    B --> C["Visit candles in order"]
    C --> D{"Open time is UTC?"}
    D -->|No| E["ArgumentException"]
    D -->|Yes| F{"Same open time as previous?"}
    F -->|Yes| G{"All candle fields equivalent?"}
    G -->|Yes| H["Skip exact duplicate"]
    G -->|No| E
    F -->|No| I{"Before expected next open?"}
    I -->|Yes| E
    I -->|No| J{"After expected next open?"}
    J -->|Yes| K["Record CandleGap"]
    J -->|No| L["Append candle"]
    K --> L
    H --> C
    L --> C
    C --> M["CandleNormalizationResult"]

    N["CandleJsonLines.WriteAsync"] --> O["Create destination directory"]
    O --> P["Write one JSON object per line"]
    Q["CandleJsonLines.ReadAsync"] --> R["Skip blank lines"]
    R --> S["Deserialize CandleEvent records in file order"]
```

## Related API Reference

- <xref:Wickd.Program>
- <xref:Wickd.Data.HistoricalDataSource>
- <xref:Wickd.Data.IMarketDataClient>
- <xref:Wickd.Data.CcxtBinanceMarketDataClient>
- <xref:Wickd.Data.HistoricalDataResult>
- <xref:Wickd.Data.CandleReplayResult>
- <xref:Wickd.Data.DatasetAliasCatalog>
- <xref:Wickd.Data.DatasetAlias>
- <xref:Wickd.Data.CandleNormalizer>
- <xref:Wickd.Data.CandleJsonLines>
- <xref:Wickd.Infrastructure.RunRequestFactory>
