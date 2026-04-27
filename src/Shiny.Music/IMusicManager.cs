namespace Shiny.Music;

/// <summary>
/// Playlist editing is not supported by Android or iOS, so we have a way you can manage it all yourself
/// Play count is also something that is only supported for iOS, so we expanded it
/// </summary>
public interface IMusicManager
{
    /// <summary>
    /// Increments the play count for the specified track by one.
    /// </summary>
    /// <param name="trackId">The platform-specific unique identifier for the track.</param>
    Task AddPlayCount(string trackId);

    /// <summary>
    /// Gets the current play count for the specified track.
    /// </summary>
    /// <param name="trackId">The platform-specific unique identifier for the track.</param>
    /// <returns>The number of times the track has been played, or 0 if never played.</returns>
    Task<int> GetPlayCount(string trackId);

    /// <summary>
    /// Gets all recorded play counts across all tracks.
    /// </summary>
    /// <returns>A read-only list of play count entries.</returns>
    Task<IReadOnlyList<PlayCount>> GetAllPlayCounts();

    /// <summary>
    /// Gets all custom playlists with their track counts.
    /// </summary>
    /// <returns>A read-only list of all playlists.</returns>
    Task<IReadOnlyList<PlaylistInfo>> GetAllPlaylists();

    /// <summary>
    /// Creates a new playlist or updates the name of an existing one.
    /// </summary>
    /// <param name="playlistId">A unique identifier for the playlist.</param>
    /// <param name="name">The display name of the playlist.</param>
    Task CreatePlaylist(string playlistId, string name);

    /// <summary>
    /// Removes a playlist and all of its associated tracks.
    /// </summary>
    /// <param name="playlistId">The unique identifier of the playlist to remove.</param>
    Task RemovePlaylist(string playlistId);

    /// <summary>
    /// Adds a track to a playlist. If the track already exists in the playlist, this is a no-op.
    /// </summary>
    /// <param name="playlistId">The unique identifier of the playlist.</param>
    /// <param name="metadata">The metadata of the track to add.</param>
    Task AddTrackToPlaylist(string playlistId, MusicMetadata metadata);

    /// <summary>
    /// Gets all tracks belonging to the specified playlist.
    /// </summary>
    /// <param name="playlistId">The unique identifier of the playlist.</param>
    /// <returns>An array of track metadata entries in the playlist.</returns>
    Task<MusicMetadata[]> GetPlaylistTracks(string playlistId);
}

/// <summary>
/// Represents the number of times a specific track has been played.
/// </summary>
/// <param name="TrackId">The platform-specific unique identifier for the track.</param>
/// <param name="Count">The total number of times the track has been played.</param>
public record PlayCount(string TrackId, int Count);
