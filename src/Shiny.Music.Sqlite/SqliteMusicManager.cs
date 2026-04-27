using Shiny.DocumentDb;

namespace Shiny.Music.Sqlite;

/// <summary>
/// Document storing the play count for a single track.
/// </summary>
/// <param name="Id">The track identifier, used as the document key.</param>
/// <param name="Count">The total number of times the track has been played.</param>
public record PlayCountDoc(string Id, int Count);

/// <summary>
/// Document representing a user-managed playlist.
/// </summary>
/// <param name="Id">The playlist identifier, used as the document key.</param>
/// <param name="Name">The display name of the playlist.</param>
public record PlaylistDoc(string Id, string Name);

/// <summary>
/// Document representing a track's membership in a playlist.
/// </summary>
/// <param name="Id">Composite key in the format "{PlaylistId}_{TrackId}".</param>
/// <param name="PlaylistId">The identifier of the parent playlist.</param>
/// <param name="Track">The full metadata of the track.</param>
public record PlaylistTrackDoc(string Id, string PlaylistId, MusicMetadata Track);

public class SqliteMusicManager(IDocumentStore data) : IMusicManager
{
    public async Task AddPlayCount(string trackId)
    {
        var existing = await data.Get<PlayCountDoc>(trackId);
        if (existing == null)
            await data.Insert(new PlayCountDoc(trackId, 1));
        else
            await data.Update(existing with { Count = existing.Count + 1 });
    }

    public async Task<int> GetPlayCount(string trackId)
    {
        var doc = await data.Get<PlayCountDoc>(trackId);
        return doc?.Count ?? 0;
    }

    public async Task<IReadOnlyList<PlayCount>> GetAllPlayCounts()
    {
        var docs = await data.Query<PlayCountDoc>().ToList();
        return docs.Select(d => new PlayCount(d.Id, d.Count)).ToList();
    }

    public async Task<IReadOnlyList<PlaylistInfo>> GetAllPlaylists()
    {
        var docs = await data.Query<PlaylistDoc>().ToList();
        var result = new List<PlaylistInfo>();
        foreach (var d in docs)
        {
            var trackCount = await data
                .Query<PlaylistTrackDoc>()
                .Where(x => x.PlaylistId == d.Id)
                .Count();
            result.Add(new PlaylistInfo(d.Id, d.Name, (int)trackCount));
        }
        return result;
    }

    public async Task CreatePlaylist(string playlistId, string name)
    {
        var existing = await data.Get<PlaylistDoc>(playlistId);
        if (existing == null)
            await data.Insert(new PlaylistDoc(playlistId, name));
        else
            await data.Update(existing with { Name = name });
    }

    public async Task RemovePlaylist(string playlistId)
    {
        await data.Remove<PlaylistDoc>(playlistId);
        await data
            .Query<PlaylistTrackDoc>()
            .Where(x => x.PlaylistId == playlistId)
            .ExecuteDelete();
    }

    public async Task AddTrackToPlaylist(string playlistId, MusicMetadata metadata)
    {
        var docId = $"{playlistId}_{metadata.Id}";
        var existing = await data.Get<PlaylistTrackDoc>(docId);
        if (existing == null)
            await data.Insert(new PlaylistTrackDoc(docId, playlistId, metadata));
    }

    public async Task<MusicMetadata[]> GetPlaylistTracks(string playlistId)
    {
        var docs = await data
            .Query<PlaylistTrackDoc>()
            .Where(x => x.PlaylistId == playlistId)
            .ToList();

        return docs.Select(d => d.Track).ToArray();
    }
}