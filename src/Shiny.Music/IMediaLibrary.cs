namespace Shiny.Music;

/// <summary>
/// Provides access to the device music library including permissions, querying, and file operations.
/// </summary>
public interface IMediaLibrary
{
    /// <summary>
    /// Requests permission to access the device music library. Prompts the user if they have not yet been asked.
    /// On Android, this requests <c>READ_MEDIA_AUDIO</c> (API 33+) or <c>READ_EXTERNAL_STORAGE</c> (older).
    /// On Apple platforms, this calls <c>MPMediaLibrary.RequestAuthorization</c>.
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
    /// Gets a single track by its platform-specific identifier.
    /// On Android, this queries <c>MediaStore.Audio.Media</c> for the matching row ID.
    /// On Apple platforms, this looks up the <c>MPMediaItem</c> with the matching persistent ID.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="trackId">The platform-specific track identifier from <see cref="MusicMetadata.Id"/>.</param>
    /// <returns>The matching <see cref="MusicMetadata"/>, or <c>null</c> if no track with that identifier exists.</returns>
    Task<MusicMetadata?> GetTrackByIdAsync(string trackId);

    /// <summary>
    /// Gets multiple tracks by their platform-specific identifiers in a single query. Results are returned in the
    /// same order as the supplied identifiers; identifiers that do not resolve to a track are omitted. Duplicate
    /// identifiers are collapsed. On Android, this issues a single <c>Id IN (...)</c> query; on Apple platforms it
    /// makes a single pass over the songs query.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="trackIds">The platform-specific track identifiers from <see cref="MusicMetadata.Id"/>.</param>
    /// <returns>A read-only list of the resolved <see cref="MusicMetadata"/>, ordered to match <paramref name="trackIds"/>.</returns>
    Task<IReadOnlyList<MusicMetadata>> GetTracksByIdsAsync(IEnumerable<string> trackIds);

    /// <summary>
    /// Copies a music file to the specified destination path.
    /// On Android, this reads from the ContentResolver input stream.
    /// On Apple platforms, this uses <c>AVAssetExportSession</c> to export the track as M4A.
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
    /// On Android, this reads from <c>MediaStore.Audio.Playlists</c> and locally-stored custom playlists.
    /// On Apple platforms, this reads from <c>MPMediaQuery.PlaylistsQuery</c> and locally-stored custom playlists.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <returns>A read-only list of <see cref="PlaylistInfo"/> for every playlist on the device, sorted alphabetically.</returns>
    Task<IReadOnlyList<PlaylistInfo>> GetPlaylistsAsync();

    /// <summary>
    /// Gets a single playlist by its identifier, including its song count. Resolves both device playlists and
    /// locally-stored custom playlists (identifiers with a <c>custom:</c> prefix).
    /// On Android, device playlists are read from <c>MediaStore.Audio.Playlists</c>; on Apple platforms from
    /// <c>MPMediaQuery.PlaylistsQuery</c>.
    /// Permission must be granted before calling this method for device playlists.
    /// </summary>
    /// <param name="playlistId">The playlist identifier returned by <see cref="GetPlaylistsAsync"/> or <see cref="CreatePlaylistAsync"/>.</param>
    /// <returns>The matching <see cref="PlaylistInfo"/>, or <c>null</c> if no playlist with that identifier exists.</returns>
    Task<PlaylistInfo?> GetPlaylistByIdAsync(string playlistId);

    /// <summary>
    /// Gets all tracks in the specified playlist.
    /// On Android, this queries <c>MediaStore.Audio.Playlists.Members</c> for the given playlist ID.
    /// On Apple platforms, this retrieves tracks from the <c>MPMediaPlaylist</c> with the matching persistent ID.
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="playlistId">The platform-specific playlist identifier returned by <see cref="GetPlaylistsAsync"/>.</param>
    /// <returns>A read-only list of <see cref="MusicMetadata"/> for every track in the playlist, in playlist order.</returns>
    Task<IReadOnlyList<MusicMetadata>> GetPlaylistTracksAsync(string playlistId);

    /// <summary>
    /// Gets the file path to the album artwork image for the specified track.
    /// On Android, this returns the content URI for the album art from MediaStore.
    /// On Apple platforms, this exports the <c>MPMediaItem.Artwork</c> image to a cached file and returns its path.
    /// Returns <c>null</c> if no artwork is available for the track.
    /// </summary>
    /// <param name="trackId">The platform-specific track identifier from <see cref="MusicMetadata.Id"/>.</param>
    /// <returns>A file path or content URI to the album artwork image, or <c>null</c> if unavailable.</returns>
    Task<string?> GetAlbumArtPathAsync(string trackId);

    /// <summary>
    /// Analyzes a track's amplitude <b>offline</b> — decoding it to PCM to measure per-window RMS and peak
    /// levels — <b>without playing it</b>. Use the result to draw a waveform / VU meter, or to locate the
    /// loud and quiet regions of a song (e.g. finding the instrumental solo) via <see cref="AudioLevels.Sections"/>.
    /// On Apple platforms the track is exported and read with <c>AVAudioFile</c>; on Android it is decoded with
    /// <c>MediaExtractor</c> + <c>MediaCodec</c>. The channels are down-mixed for a single-envelope result.
    /// <para>
    /// Returns <c>null</c> when the track cannot be decoded to PCM — DRM-protected Apple Music content and
    /// streaming catalog items (empty <see cref="MusicMetadata.ContentUri"/>), the same tracks
    /// <see cref="CopyTrackAsync"/> refuses — or when the track id does not resolve. For those tracks, the
    /// time-synced-lyric approach (<see cref="LyricsExtensions.GetInstrumentalGaps"/>) still works, as it
    /// needs no audio decode.
    /// </para>
    /// Permission must be granted before calling this method.
    /// </summary>
    /// <param name="trackId">The platform-specific track identifier from <see cref="MusicMetadata.Id"/>.</param>
    /// <param name="window">
    /// The resolution of the analysis — the duration each RMS/peak entry represents. Smaller windows give a
    /// finer waveform at the cost of more entries. Defaults to 500&#160;ms when <c>null</c>.
    /// </param>
    /// <returns>
    /// The <see cref="AudioLevels"/> envelope and derived sections, or <c>null</c> if the track is
    /// DRM-protected / streaming-only or does not resolve.
    /// </returns>
    Task<AudioLevels?> AnalyzeLevelsAsync(string trackId, TimeSpan? window = null);

    /// <summary>
    /// Checks whether the user has an active music streaming subscription that allows catalog playback.
    /// On Apple platforms, this checks for Apple Music subscription capability via MusicKit <c>MusicSubscription</c>.
    /// On Android, this always returns <c>false</c>.
    /// </summary>
    /// <returns><c>true</c> if the user can play streaming catalog content; otherwise <c>false</c>.</returns>
    Task<bool> HasStreamingSubscriptionAsync();

    /// <summary>
    /// Searches the Apple Music streaming catalog for songs matching the given term. Unlike
    /// <see cref="SearchTracksAsync"/> (which searches the user's <em>local</em> library), this returns
    /// streaming catalog tracks that need not be in the user's library. Each returned
    /// <see cref="MusicMetadata"/> carries a <see cref="MusicMetadata.CatalogId"/> and can be played
    /// through <see cref="IMusicPlayer.PlayAsync"/>, provided the user has an active subscription
    /// (see <see cref="HasStreamingSubscriptionAsync"/>). Catalog tracks are streaming-only: their
    /// <see cref="MusicMetadata.ContentUri"/> is empty and they cannot be copied via <see cref="CopyTrackAsync"/>.
    /// The first call triggers a MusicKit authorization prompt if the user has not yet been asked.
    /// <para><b>Apple platforms only.</b> Throws <see cref="PlatformNotSupportedException"/> on Android.</para>
    /// </summary>
    /// <param name="term">The search text to match against catalog song title, artist, and album.</param>
    /// <param name="limit">The maximum number of results to return. Apple Music caps this at 25; defaults to 25.</param>
    /// <returns>
    /// A read-only list of catalog <see cref="MusicMetadata"/> matching the term. Returns an empty list when
    /// nothing matches, the user has no subscription/authorization, or the request fails.
    /// </returns>
    Task<IReadOnlyList<MusicMetadata>> SearchCatalogAsync(string term, int limit = 25);

    /// <summary>
    /// Creates a new locally-stored custom playlist with the given name.
    /// On both platforms, playlists are persisted as JSON in local app data.
    /// </summary>
    /// <param name="name">The display name of the playlist.</param>
    /// <returns>The created playlist info.</returns>
    Task<PlaylistInfo> CreatePlaylistAsync(string name);

    /// <summary>
    /// Removes a custom playlist by its identifier.
    /// </summary>
    /// <param name="playlistId">The custom playlist identifier.</param>
    Task RemovePlaylistAsync(string playlistId);

    /// <summary>
    /// Adds a track to an existing custom playlist. No-op if the track already exists in the playlist.
    /// </summary>
    /// <param name="playlistId">The custom playlist identifier.</param>
    /// <param name="track">The track to add.</param>
    Task AddTrackToPlaylistAsync(string playlistId, MusicMetadata track);

    /// <summary>
    /// Removes a track from an existing custom playlist.
    /// </summary>
    /// <param name="playlistId">The custom playlist identifier.</param>
    /// <param name="trackId">The platform-specific track identifier to remove.</param>
    Task RemoveTrackFromPlaylistAsync(string playlistId, string trackId);

}
