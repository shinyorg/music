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
    /// Gets all distinct genre names from the user's music library, sorted alphabetically.
    /// Tracks with no genre set are excluded from the results.
    /// Permission must be granted before calling this method.
    /// On Android, this queries the <c>MediaStore.Audio.Genres</c> table.
    /// On iOS, this uses <c>MPMediaQuery.GenresQuery</c> to enumerate genre collections.
    /// </summary>
    /// <returns>A read-only list of distinct, non-null genre names sorted alphabetically.</returns>
    Task<IReadOnlyList<string>> GetGenresAsync();

    /// <summary>
    /// Checks whether the user has an active music streaming subscription that allows catalog playback.
    /// On iOS, this checks for Apple Music subscription capability via <c>SKCloudServiceController</c>.
    /// On Android, this always returns <c>false</c>.
    /// </summary>
    /// <returns><c>true</c> if the user can play streaming catalog content; otherwise <c>false</c>.</returns>
    Task<bool> HasStreamingSubscriptionAsync();
}
