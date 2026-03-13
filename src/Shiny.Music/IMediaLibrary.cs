namespace Shiny.Music;

/// <summary>
/// Provides access to the device music library including permissions, querying, and file operations.
/// </summary>
public interface IMediaLibrary
{
    /// <summary>
    /// Requests permission to access the device music library. Prompts the user if they have not yet been asked.
    /// On Android, this requests <c>READ_MEDIA_AUDIO</c> (API 33+) or <c>READ_EXTERNAL_STORAGE</c> (older).
    /// On iOS, this calls <c>MPMediaLibrary.RequestAuthorization</c>.
    /// </summary>
    /// <returns>The resulting <see cref="PermissionStatus"/> after the user responds to the prompt.</returns>
    Task<PermissionStatus> RequestPermissionAsync();

    /// <summary>
    /// Checks the current permission status without prompting the user.
    /// </summary>
    /// <returns>The current <see cref="PermissionStatus"/>.</returns>
    Task<PermissionStatus> CheckPermissionAsync();

    /// <summary>
    /// Gets all music tracks available on the device.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <returns>A read-only list of <see cref="MusicMetadata"/> for every music track on the device.</returns>
    Task<IReadOnlyList<MusicMetadata>> GetAllTracksAsync();

    /// <summary>
    /// Searches for tracks matching the given query string (matches title, artist, or album).
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="query">The search text to match against track title, artist, or album name.</param>
    /// <returns>A read-only list of <see cref="MusicMetadata"/> for tracks matching the query.</returns>
    Task<IReadOnlyList<MusicMetadata>> SearchTracksAsync(string query);

    /// <summary>
    /// Gets tracks matching the specified filter criteria. All non-null filter properties are combined with AND logic.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="filter">The filter criteria to apply. See <see cref="MusicFilter"/> for available options.</param>
    /// <returns>A read-only list of <see cref="MusicMetadata"/> for tracks matching all specified filter criteria.</returns>
    Task<IReadOnlyList<MusicMetadata>> GetTracksAsync(MusicFilter filter);

    /// <summary>
    /// Copies a music file to the specified destination path.
    /// On Android, this reads from the ContentResolver input stream.
    /// On iOS, this uses <c>AVAssetExportSession</c> to export the track as M4A.
    /// DRM-protected Apple Music tracks cannot be copied and will return <c>false</c>.
    /// </summary>
    /// <param name="track">The track to copy. Check that <see cref="MusicMetadata.ContentUri"/> is not empty before calling.</param>
    /// <param name="destinationPath">The full file path where the track should be written. Parent directories will be created if needed.</param>
    /// <returns><c>true</c> if the file was copied successfully; <c>false</c> if the track is DRM-protected or the operation failed.</returns>
    Task<bool> CopyTrackAsync(MusicMetadata track, string destinationPath);

    /// <summary>
    /// Gets all distinct genre names from the user's music library with track counts, sorted alphabetically.
    /// Tracks with no genre set are excluded from the results.
    /// When a <paramref name="filter"/> is provided, only tracks matching the filter criteria are considered.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="filter">Optional filter to narrow the tracks considered for genre grouping (e.g., filter by decade to see genres within that decade).</param>
    /// <returns>A read-only list of distinct, non-null genre names with their track counts, sorted alphabetically.</returns>
    Task<IReadOnlyList<GroupedCount<string>>> GetGenresAsync(MusicFilter? filter = null);

    /// <summary>
    /// Gets all distinct release years from the user's music library with track counts, sorted in ascending order.
    /// Tracks with no year metadata are excluded from the results.
    /// When a <paramref name="filter"/> is provided, only tracks matching the filter criteria are considered.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="filter">Optional filter to narrow the tracks considered for year grouping (e.g., filter by genre to see years within that genre).</param>
    /// <returns>A read-only list of distinct, non-zero release years with their track counts, sorted in ascending order.</returns>
    Task<IReadOnlyList<GroupedCount<int>>> GetYearsAsync(MusicFilter? filter = null);

    /// <summary>
    /// Gets all distinct decades represented in the user's music library with track counts, sorted in ascending order.
    /// Each decade is returned as its starting year (e.g., 1990 represents the 1990s).
    /// Tracks with no year metadata are excluded from the results.
    /// When a <paramref name="filter"/> is provided, only tracks matching the filter criteria are considered.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="filter">Optional filter to narrow the tracks considered for decade grouping (e.g., filter by genre to see decades within that genre).</param>
    /// <returns>A read-only list of distinct decade start years (e.g., 1970, 1980, 1990) with their track counts, sorted in ascending order.</returns>
    Task<IReadOnlyList<GroupedCount<int>>> GetDecadesAsync(MusicFilter? filter = null);

    /// <summary>
    /// Gets all playlists from the device music library with their song counts, sorted alphabetically by name.
    /// On Android, this reads from <c>MediaStore.Audio.Playlists</c>.
    /// On iOS, this reads from <c>MPMediaQuery.PlaylistsQuery</c>.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <returns>A read-only list of <see cref="PlaylistInfo"/> for every playlist on the device, sorted alphabetically.</returns>
    Task<IReadOnlyList<PlaylistInfo>> GetPlaylistsAsync();

    /// <summary>
    /// Gets all tracks in the specified playlist.
    /// On Android, this queries <c>MediaStore.Audio.Playlists.Members</c> for the given playlist ID.
    /// On iOS, this retrieves tracks from the <c>MPMediaPlaylist</c> with the matching persistent ID.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="playlistId">The platform-specific playlist identifier returned by <see cref="GetPlaylistsAsync"/>.</param>
    /// <returns>A read-only list of <see cref="MusicMetadata"/> for every track in the playlist, in playlist order.</returns>
    Task<IReadOnlyList<MusicMetadata>> GetPlaylistTracksAsync(string playlistId);

    /// <summary>
    /// Creates a new empty playlist with the specified name.
    /// On Android, inserts a row into <c>MediaStore.Audio.Playlists</c>.
    /// On iOS, creates a playlist via <c>MPMediaLibrary.GetDefaultMediaLibrary()</c>.
    /// Requires write access to the media library. On Android, the app must hold
    /// <c>WRITE_EXTERNAL_STORAGE</c> (API &lt; 30) or use scoped storage (API 30+).
    /// </summary>
    /// <param name="name">The display name for the new playlist. Must not be null or whitespace.</param>
    /// <returns>
    /// A <see cref="PlaylistInfo"/> for the newly created playlist with a <see cref="PlaylistInfo.SongCount"/> of 0,
    /// or <c>null</c> if creation failed.
    /// </returns>
    Task<PlaylistInfo?> CreatePlaylistAsync(string name);

    /// <summary>
    /// Renames an existing playlist.
    /// On Android, updates the <c>name</c> column in <c>MediaStore.Audio.Playlists</c>.
    /// On iOS, playlist renaming is not supported by the public <c>MediaPlayer</c> framework API and always returns <c>false</c>.
    /// </summary>
    /// <param name="playlistId">The platform-specific playlist identifier returned by <see cref="GetPlaylistsAsync"/>.</param>
    /// <param name="newName">The new display name for the playlist. Must not be null or whitespace.</param>
    /// <returns><c>true</c> if the rename succeeded; <c>false</c> otherwise, including when not supported on the current platform.</returns>
    Task<bool> RenamePlaylistAsync(string playlistId, string newName);

    /// <summary>
    /// Deletes a playlist from the device music library.
    /// On Android, deletes the row from <c>MediaStore.Audio.Playlists</c> along with all its member entries.
    /// On Android 10+, deleting a playlist owned by another app will fail with a <c>SecurityException</c> and return <c>false</c>.
    /// On iOS, playlist deletion is not supported by the public <c>MediaPlayer</c> framework API and always returns <c>false</c>.
    /// </summary>
    /// <param name="playlistId">The platform-specific playlist identifier returned by <see cref="GetPlaylistsAsync"/>.</param>
    /// <returns><c>true</c> if the playlist was deleted; <c>false</c> otherwise, including when not supported on the current platform.</returns>
    Task<bool> DeletePlaylistAsync(string playlistId);

    /// <summary>
    /// Appends one or more tracks to the end of the specified playlist.
    /// On Android, bulk-inserts rows into <c>MediaStore.Audio.Playlists.Members</c> after the current last member.
    /// On iOS, calls <c>MPMediaPlaylist.AddItemAsync</c> for each track using its <see cref="MusicMetadata.StoreId"/>.
    /// Because the iOS API requires an Apple Music catalog product ID, only tracks with a non-empty
    /// <see cref="MusicMetadata.StoreId"/> can be added on iOS; local-only tracks are silently skipped.
    /// </summary>
    /// <param name="playlistId">The platform-specific playlist identifier returned by <see cref="GetPlaylistsAsync"/>.</param>
    /// <param name="tracks">The tracks to append to the playlist, in the order they should appear.</param>
    /// <returns><c>true</c> if at least one track was successfully added; <c>false</c> if nothing was added or the operation failed.</returns>
    Task<bool> AddTracksToPlaylistAsync(string playlistId, IEnumerable<MusicMetadata> tracks);

    /// <summary>
    /// Removes a single track from the specified playlist.
    /// On Android, deletes the member row matching the given <paramref name="trackId"/> from <c>MediaStore.Audio.Playlists.Members</c>.
    /// On iOS, track removal from playlists is not supported by the public <c>MediaPlayer</c> framework API and always returns <c>false</c>.
    /// </summary>
    /// <param name="playlistId">The platform-specific playlist identifier returned by <see cref="GetPlaylistsAsync"/>.</param>
    /// <param name="trackId">The <see cref="MusicMetadata.Id"/> of the track to remove.</param>
    /// <returns><c>true</c> if the track was removed; <c>false</c> otherwise, including when not supported on the current platform.</returns>
    Task<bool> RemoveTrackFromPlaylistAsync(string playlistId, string trackId);

    /// <summary>
    /// Checks whether the user has an active music streaming subscription that allows catalog playback.
    /// On iOS, this checks for Apple Music subscription capability via <c>SKCloudServiceController</c>.
    /// On Android, this always returns <c>false</c>.
    /// </summary>
    /// <returns><c>true</c> if the user can play streaming catalog content; otherwise <c>false</c>.</returns>
    Task<bool> HasStreamingSubscriptionAsync();
}
