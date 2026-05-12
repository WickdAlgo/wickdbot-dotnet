---
title: Programmatic Business Flows
---

# Programmatic Business Flows

These diagrams show the currently implemented WickdBot program paths. Placeholder commands are shown as placeholders, not future behavior.

## CLI Command Flow

```mermaid
flowchart TD
    A["Program.Main(args)"] --> B["BuildRootCommand()"]
    B --> C{"Subcommand"}
    C --> D["fetch"]
    C --> E["backtest"]
    C --> F["analyze"]
    D --> G["ResolveRunRequest"]
    E --> G
    F --> H["Read required --run-id"]
    G --> I{"Request valid?"}
    I -->|No| J["Write validation error"]
    J --> K["Exit 2"]
    I -->|Yes| L["ReturnNotImplemented(command)"]
    H --> L
    L --> M["Write placeholder message"]
    M --> N["Exit 1"]
```

## Configuration And Run Request Flow

```mermaid
flowchart TD
    A["ResolveRunRequest"] --> B["WickdBotConfigurationLoader.LoadDefault()"]
    B --> C{"Find appsettings.json"}
    C -->|App output| D["Use AppContext.BaseDirectory"]
    C -->|Repository root| E["Use src/WickdBot/appsettings.json"]
    D --> F["Parse WickdBot settings"]
    E --> F
    F --> G["Resolve markets.json path"]
    G --> H["Load market definitions"]
    H --> I{"Default market configured?"}
    I -->|No| J["WickdBotConfigurationException"]
    I -->|Yes| K["RunRequestFactory.Create"]
    K --> L["Resolve requested market"]
    L --> M["Parse timeframe"]
    M --> N["Validate UTC date range"]
    N --> O["Build deterministic candle cache path"]
    O --> P["RunRequest"]
```

## Candle Normalization Flow

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
```

## JSONL Candle Cache Flow

```mermaid
flowchart LR
    A["WriteAsync(path, candles)"] --> B["Resolve destination directory"]
    B --> C["Create directory when needed"]
    C --> D["Create file"]
    D --> E["Serialize each CandleEvent"]
    E --> F["Write one JSON object per line"]

    G["ReadAsync(path)"] --> H["Open JSONL file"]
    H --> I["Read each line"]
    I --> J{"Blank line?"}
    J -->|Yes| I
    J -->|No| K["Deserialize CandleEvent"]
    K --> L["Append to result list"]
    L --> I
    I --> M["Return candles in file order"]
```

## Related API Reference

- <xref:WickdBot.Program>
- <xref:WickdBot.Infrastructure.WickdBotConfigurationLoader>
- <xref:WickdBot.Infrastructure.RunRequestFactory>
- <xref:WickdBot.Data.CandleNormalizer>
- <xref:WickdBot.Data.CandleJsonLines>
