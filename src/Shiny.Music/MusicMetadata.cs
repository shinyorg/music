namespace Shiny.Music;

/// <summary>
/// Represents metadata for a music track on the device.
/// </summary>
/// <param name="Id">Platform-specific unique identifier for the track. On Android this is the MediaStore row ID; on iOS it is the persistent ID.</param>
/// <param name="Title">The title of the track, or <c>null</c> if not available.</param>
/// <param name="Artist">The artist or performer of the track, or <c>null</c> if not available.</param>
/// <param name="Album">The album the track belongs to, or <c>null</c> if not available.</param>
/// <param name="Genre">The genre of the track, or <c>null</c> if not available.</param>
/// <param name="Duration">The playback duration of the track.</param>
/// <param name="AlbumArtUri">A URI pointing to the album artwork image. Available on Android via MediaStore; <c>null</c> on iOS where artwork is accessed through MPMediaItem.Artwork.</param>
/// <param name="IsExplicit">
/// Indicates whether the track is marked as explicit content.
/// <c>true</c> if explicit, <c>false</c> if not, <c>null</c> if the platform does not provide this information.
/// Currently only available on iOS via <c>MPMediaItem.IsExplicitItem</c>; always <c>null</c> on Android.
/// </param>
/// <param name="ContentUri">
/// A URI that can be used for playback and file operations.
/// On Android this is a <c>content://</c> URI from MediaStore.
/// On iOS this is the <c>ipod-library://</c> asset URL from MPMediaItem.AssetURL.
/// This value is <see cref="string.Empty"/> for DRM-protected Apple Music subscription tracks,
/// which cannot be played via AVAudioPlayer or copied.
/// </param>
/// <param name="StoreId">
/// An optional provider-specific store identifier used for streaming playback.
/// On iOS, this is the Apple Music catalog ID (from <c>PlayParams.Id</c>) and enables playback
/// via <c>MPMusicPlayerController</c> for Apple Music subscription content.
/// This is ignored on Android.
/// </param>
/// <param name="Year">
/// The release year of the track, or <c>null</c> if the platform does not provide this information
/// or the track has no year metadata set.
/// On Android this comes from <c>MediaStore.Audio.Media.YEAR</c>; on iOS from <c>MPMediaItem.Year</c>.
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
    int? Year = null
)
{
    /// <summary>
    /// Whether this track can be played. A track is playable if it has a <see cref="ContentUri"/> (local)
    /// or a <see cref="StoreId"/> (streaming via Apple Music).
    /// </summary>
    public bool IsPlayable => !string.IsNullOrEmpty(ContentUri) || !string.IsNullOrEmpty(StoreId);
}
