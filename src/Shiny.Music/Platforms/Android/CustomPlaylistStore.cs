using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shiny.Music;

record CustomPlaylist(string Id, string Name, MusicMetadata[] Tracks);

[JsonSerializable(typeof(List<CustomPlaylist>))]
[JsonSerializable(typeof(MusicMetadata))]
partial class CustomPlaylistJsonContext : JsonSerializerContext;

class CustomPlaylistStore
{
    const string CustomIdPrefix = "custom:";
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "custom_playlists.json"
    );

    readonly SemaphoreSlim sync = new(1, 1);

    public static bool IsCustomPlaylistId(string id) => id.StartsWith(CustomIdPrefix);
    public static string NewId() => CustomIdPrefix + Guid.NewGuid().ToString("N");

    public async Task<List<CustomPlaylist>> LoadAllAsync()
    {
        await this.sync.WaitAsync();
        try
        {
            return await ReadFileAsync();
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task<CustomPlaylist> CreateAsync(string name)
    {
        await this.sync.WaitAsync();
        try
        {
            var playlists = await ReadFileAsync();
            var playlist = new CustomPlaylist(NewId(), name, []);
            playlists.Add(playlist);
            await WriteFileAsync(playlists);
            return playlist;
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task RemoveAsync(string playlistId)
    {
        await this.sync.WaitAsync();
        try
        {
            var playlists = await ReadFileAsync();
            playlists.RemoveAll(p => p.Id == playlistId);
            await WriteFileAsync(playlists);
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task<CustomPlaylist?> GetByIdAsync(string playlistId)
    {
        await this.sync.WaitAsync();
        try
        {
            var playlists = await ReadFileAsync();
            return playlists.FirstOrDefault(p => p.Id == playlistId);
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task<MusicMetadata[]> GetTracksAsync(string playlistId)
    {
        await this.sync.WaitAsync();
        try
        {
            var playlists = await ReadFileAsync();
            var playlist = playlists.FirstOrDefault(p => p.Id == playlistId);
            return playlist?.Tracks ?? [];
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task AddTrackAsync(string playlistId, MusicMetadata track)
    {
        await this.sync.WaitAsync();
        try
        {
            var playlists = await ReadFileAsync();
            var index = playlists.FindIndex(p => p.Id == playlistId);
            if (index < 0)
                throw new InvalidOperationException($"Playlist '{playlistId}' not found.");

            var playlist = playlists[index];
            if (playlist.Tracks.Any(t => t.Id == track.Id))
                return;

            playlists[index] = playlist with { Tracks = [.. playlist.Tracks, track] };
            await WriteFileAsync(playlists);
        }
        finally
        {
            this.sync.Release();
        }
    }

    public async Task RemoveTrackAsync(string playlistId, string trackId)
    {
        await this.sync.WaitAsync();
        try
        {
            var playlists = await ReadFileAsync();
            var index = playlists.FindIndex(p => p.Id == playlistId);
            if (index < 0)
                throw new InvalidOperationException($"Playlist '{playlistId}' not found.");

            var playlist = playlists[index];
            playlists[index] = playlist with
            {
                Tracks = playlist.Tracks.Where(t => t.Id != trackId).ToArray()
            };
            await WriteFileAsync(playlists);
        }
        finally
        {
            this.sync.Release();
        }
    }

    static async Task<List<CustomPlaylist>> ReadFileAsync()
    {
        if (!File.Exists(FilePath))
            return [];

        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync(
            stream,
            CustomPlaylistJsonContext.Default.ListCustomPlaylist
        ) ?? [];
    }

    static async Task WriteFileAsync(List<CustomPlaylist> playlists)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(
            stream,
            playlists,
            CustomPlaylistJsonContext.Default.ListCustomPlaylist
        );
    }
}
