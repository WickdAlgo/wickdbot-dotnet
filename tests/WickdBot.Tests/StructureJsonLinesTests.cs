#nullable enable

using WickdBot.Data;
using WickdBot.Engines;

namespace WickdBot.Tests;

/// <summary>
/// Verifies JSON Lines persistence for structure event records.
/// </summary>
public class StructureJsonLinesTests
{
    /// <summary>
    /// Confirms JSONL round-tripping preserves structure event identity and decimal values.
    /// </summary>
    [Fact]
    public async Task RoundTripPreservesStructureEventValues()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.DirectoryPath, "structures.jsonl");
        var structureEvent = new StructureEvent(
            "phase-3-test",
            sequence: 1,
            eventId: "structure-000001",
            StructureEventType.BuySideSweepCandidate,
            observedOpenTimeUtc: new DateTimeOffset(2026, 5, 6, 0, 15, 0, TimeSpan.Zero),
            subjectOpenTimeUtc: new DateTimeOffset(2026, 5, 6, 0, 10, 0, TimeSpan.Zero),
            marketId: "BTC_USDT_PERP",
            exchangeId: "binance",
            timeframe: "5m",
            entityId: "breach-000001",
            relatedEntityIds: ["swing-000001", "swing-000003"],
            direction: StructureDirection.Bearish,
            zoneLow: 101.12345678m,
            zoneHigh: 102.87654321m,
            fillPercent: 25.5m,
            breachedLiquidityId: "swing-000001",
            originalBreachEventId: "structure-000001",
            protectedSwingId: "swing-000003",
            classificationStage: "sweepCandidate",
            sourceOpenTimesUtc:
            [
                new DateTimeOffset(2026, 5, 6, 0, 5, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 6, 0, 10, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 6, 0, 15, 0, TimeSpan.Zero)
            ]);

        await StructureJsonLines.WriteAsync(path, [structureEvent]);

        var events = await StructureJsonLines.ReadAsync(path);
        var actual = Assert.Single(events);

        Assert.Equal(structureEvent.RunId, actual.RunId);
        Assert.Equal(structureEvent.EventId, actual.EventId);
        Assert.Equal(structureEvent.EventType, actual.EventType);
        Assert.Equal(structureEvent.EntityId, actual.EntityId);
        Assert.Equal(structureEvent.RelatedEntityIds, actual.RelatedEntityIds);
        Assert.Equal(structureEvent.Direction, actual.Direction);
        Assert.Equal(structureEvent.ZoneLow, actual.ZoneLow);
        Assert.Equal(structureEvent.ZoneHigh, actual.ZoneHigh);
        Assert.Equal(structureEvent.FillPercent, actual.FillPercent);
        Assert.Equal(structureEvent.BreachedLiquidityId, actual.BreachedLiquidityId);
        Assert.Equal(structureEvent.OriginalBreachEventId, actual.OriginalBreachEventId);
        Assert.Equal(structureEvent.ProtectedSwingId, actual.ProtectedSwingId);
        Assert.Equal(structureEvent.ClassificationStage, actual.ClassificationStage);
        Assert.Equal(structureEvent.SourceOpenTimesUtc, actual.SourceOpenTimesUtc);
    }
}
