namespace Shiny.Music;

/// <summary>
/// Represents metadata for a music track on the device.
/// </summary>
/// <param name="Id">Platform-specific unique identifier for the track. On Android this is the MediaStore row ID; on Apple platforms it is the <c>MPMediaItem.PersistentID</c>.</param>
/// <param name="Title">The title of the track, or <c>null</c> if not available.</param>
/// <param name="Artist">The artist or performer of the track, or <c>null</c> if not available.</param>
/// <param name="Album">The album the track belongs to, or <c>null</c> if not available.</param>
/// <param name="Genre">The genre of the track, or <c>null</c> if not available. On Android this comes from <c>MediaStore.Audio.Genres</c>; on Apple platforms from <c>MPMediaItem.Genre</c>.</param>
/// <param name="Duration">The playback duration of the track.</param>
/// <param name="AlbumArtUri">A URI pointing to the album artwork image. Available on Android via MediaStore; <c>null</c> on Apple platforms where artwork is accessed through <see cref="IMediaLibrary.GetAlbumArtPathAsync"/>.</param>
/// <param name="IsExplicit">
/// Indicates whether the track is marked as explicit content.
/// <c>true</c> if explicit, <c>false</c> if not, <c>null</c> if the platform does not provide this information.
/// Available on Apple platforms via <c>MPMediaItem.IsExplicitItem</c>; always <c>null</c> on Android.
/// </param>
/// <param name="ContentUri">
/// A URI that can be used for playback and file operations.
/// On Android this is a <c>content://</c> URI from MediaStore.
/// On Apple platforms this is the <c>ipod-library://</c> asset URL from <c>MPMediaItem.AssetURL</c>.
/// This value is <see cref="string.Empty"/> for DRM-protected Apple Music subscription tracks,
/// which cannot be copied via <c>AVAssetExportSession</c>.
/// </param>
/// <param name="StoreId">
/// An optional track identifier used for playback via <c>MPMusicPlayerController</c>.
/// On Apple platforms, this is the <c>MPMediaItem.PersistentID</c>.
/// Always <c>null</c> on Android.
/// </param>
/// <param name="CatalogId">
/// The Apple Music streaming catalog identifier for tracks returned by
/// <see cref="IMediaLibrary.SearchCatalogAsync"/>. When set, the track is a streaming catalog item
/// (not in the user's library): <see cref="IMusicPlayer.PlayAsync"/> enqueues it by this id via
/// <c>MPMusicPlayerStoreQueueDescriptor</c>, and its <see cref="ContentUri"/> is empty (streaming-only,
/// not copyable). <c>null</c> for local library tracks and always <c>null</c> on Android.
/// </param>
/// <param name="Year">
/// The release year of the track, or <c>null</c> if the platform does not provide this information
/// or the track has no year metadata set.
/// On Android this comes from <c>MediaStore.Audio.Media.YEAR</c>; on Apple platforms from <c>MPMediaItem.ReleaseDate</c>.
/// </param>
/// <param name="PlayCount">
/// The number of times this track has been played. On Apple platforms this comes from <c>MPMediaItem.PlayCount</c>;
/// on Android this is tracked locally and incremented when <c>IMusicPlayer.PlayAsync</c> is called.
/// </param>
public record MusicMetadata(
    string Id,
    string? Title,
    string? Artist,
    string? Album,
    string? Genre,
    TimeSpan Duration,
    string? AlbumArtUri,
    bool? IsExplicit,
    string ContentUri,
    string? StoreId = null,
    int? Year = null,
    int PlayCount = 0,
    string? CatalogId = null
)
{
    /// <summary>
    /// Whether this track can be played. A track is playable if it has a <see cref="ContentUri"/> (local),
    /// a <see cref="StoreId"/> (local playback via <c>MPMusicPlayerController</c>), or a
    /// <see cref="CatalogId"/> (streaming catalog playback, subscription required).
    /// </summary>
    public bool IsPlayable =>
        !string.IsNullOrEmpty(ContentUri) ||
        !string.IsNullOrEmpty(StoreId) ||
        !string.IsNullOrEmpty(CatalogId);
}
