using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiny.Music;

record PlayCountEntry(string TrackId, int Count);

[JsonSerializable(typeof(List<PlayCountEntry>))]
partial class PlayCountJsonContext : JsonSerializerContext;

public class PlayCountStore
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "play_counts.json"
    );

    readonly SemaphoreSlim sync = new(1, 1);

    public async Task<int> IncrementAsync(string trackId)
    {
        await this.sync.WaitAsync();
        try
        {
            var entries = await ReadFileAsync();
            var index = entries.FindIndex(e => e.TrackId == trackId);

            int newCount;
            if (index >= 0)
            {
                newCount = entries[index].Count + 1;
                entries[index] = entries[index] with { Count = newCount };
            }
            else
            {
                newCount = 1;
                entries.Add(new PlayCountEntry(trackId, 1));
            }

            await WriteFileAsync(entries);
            return newCount;
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task<Dictionary<string, int>> LoadAllAsync()
    {
        await this.sync.WaitAsync();
        try
        {
            var entries = await ReadFileAsync();
            return entries.ToDictionary(e => e.TrackId, e => e.Count);
        }
        finally
        {
            this.sync.Release();
        }
    }

    static async Task<List<PlayCountEntry>> ReadFileAsync()
    {
        if (!File.Exists(FilePath))
            return [];

        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync(
            stream,
            PlayCountJsonContext.Default.ListPlayCountEntry
        ) ?? [];
    }

    static async Task WriteFileAsync(List<PlayCountEntry> entries)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(
            stream,
            entries,
            PlayCountJsonContext.Default.ListPlayCountEntry
        );
    }
}
