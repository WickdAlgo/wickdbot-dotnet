using System.Text.Json;
using System.Text.Json.Serialization;
using Wickd.Models;

namespace Wickd.Data;

/// <summary>
/// Reads and writes normalized candle events as deterministic JSON Lines.
/// </summary>
internal static class CandleJsonLines
{
    /// <summary>
    /// Serializer options shared by candle JSONL read and write paths.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Writes candles to a JSONL file using one serialized candle per line.
    /// </summary>
    /// <param name="path">Destination JSONL path.</param>
    /// <param name="candles">Candles to write.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>A task that completes after the file is written.</returns>
    internal static async Task WriteAsync(
        string path,
        IEnumerable<CandleEvent> candles,
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

        foreach (var candle in candles)
        {
            var json = JsonSerializer.Serialize(candle, SerializerOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// Reads candles from a JSONL file.
    /// </summary>
    /// <param name="path">Source JSONL path.</param>
    /// <param name="cancellationToken">Cancellation token for file I/O.</param>
    /// <returns>The candle events in file order.</returns>
    internal static async Task<IReadOnlyList<CandleEvent>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var candles = new List<CandleEvent>();

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var candle = JsonSerializer.Deserialize<CandleEvent>(line, SerializerOptions)
                ?? throw new JsonException("Candle JSONL line deserialized to null.");

            candles.Add(candle);
        }

        return candles;
    }

    /// <summary>
    /// Creates deterministic JSON serializer options for candle JSONL records.
    /// </summary>
    /// <returns>The serializer options used for candle records.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
