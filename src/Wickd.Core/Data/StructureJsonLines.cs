#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using Wickd.Engines;

namespace Wickd.Data;

/// <summary>
/// Reads and writes structure events as deterministic JSON Lines.
/// </summary>
internal static class StructureJsonLines
{
    /// <summary>
    /// Serializer options shared by structure JSONL read and write paths.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Writes structure events to a JSONL file using one serialized event per line.
    /// </summary>
    /// <param name="path">Destination JSONL path.</param>
    /// <param name="events">Structure events to write.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>A task that completes after the file is written.</returns>
    internal static async Task WriteAsync(
        string path,
        IEnumerable<StructureEvent> events,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream)
        {
            NewLine = "\n"
        };

        foreach (var structureEvent in events)
        {
            var json = JsonSerializer.Serialize(structureEvent, SerializerOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// Reads structure events from a JSONL file.
    /// </summary>
    /// <param name="path">Source JSONL path.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The structure events in file order.</returns>
    internal static async Task<IReadOnlyList<StructureEvent>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var events = new List<StructureEvent>();

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var structureEvent = JsonSerializer.Deserialize<StructureEvent>(line, SerializerOptions)
                ?? throw new JsonException("Structure JSONL line deserialized to null.");

            events.Add(structureEvent);
        }

        return events;
    }

    /// <summary>
    /// Creates deterministic JSON serializer options for structure records.
    /// </summary>
    /// <returns>The serializer options used for structure records.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
