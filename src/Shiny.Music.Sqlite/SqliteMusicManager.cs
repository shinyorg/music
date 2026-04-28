using Shiny.DocumentDb;

namespace Shiny.Music.Sqlite;

/// <summary>
/// Document representing a user-managed playlist with embedded tracks.
/// </summary>
/// <param name="Id">The playlist identifier, used as the document key.</param>
/// <param name="Name">The display name of the playlist.</param>
/// <param name="Tracks">The tracks in the playlist.</param>
public record PlaylistDoc(string Id, string Name, MusicMetadata[] Tracks);

public class SqliteMusicManager(IDocumentStore data) : IMusicManager
{
    public async Task AddPlayCount(string trackId)
    {
        var existing = await data.Get<PlayCount>(trackId);
        if (existing == null)
            await data.Insert(new PlayCount(trackId, 1));
        else
            await data.Update(existing with { Count = existing.Count + 1 });
    }

    public async Task<int> GetPlayCount(string trackId)
    {
        var doc = await data.Get<PlayCount>(trackId);
        return doc?.Count ?? 0;
    }

    public async Task<IReadOnlyList<PlayCount>> GetAllPlayCounts()
    {
        var docs = await data.Query<PlayCount>().ToList();
        return docs;
    }

    public async Task<IReadOnlyList<PlaylistInfo>> GetAllPlaylists()
    {
        var docs = await data.Query<PlaylistDoc>().ToList();
        return docs
            .Select(d => new PlaylistInfo(d.Id, d.Name, d.Tracks.Length))
            .ToList();
    }

    public async Task CreatePlaylist(string playlistId, string name)
    {
        var existing = await data.Get<PlaylistDoc>(playlistId);
        if (existing == null)
            await data.Insert(new PlaylistDoc(playlistId, name, []));
        else
            await data.Update(existing with { Name = name });
    }

    public async Task RemovePlaylist(string playlistId)
    {
        await data.Remove<PlaylistDoc>(playlistId);
    }

    public async Task AddTrackToPlaylist(string playlistId, MusicMetadata metadata)
    {
        var playlist = await data.Get<PlaylistDoc>(playlistId);
        if (playlist == null) return;

        if (playlist.Tracks.Any(t => t.Id == metadata.Id))
            return;

        await data.Update(playlist with { Tracks = [..playlist.Tracks, metadata] });
    }

    public async Task<MusicMetadata[]> GetPlaylistTracks(string playlistId)
    {
        var playlist = await data.Get<PlaylistDoc>(playlistId);
        return playlist?.Tracks ?? [];
    }
}