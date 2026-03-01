namespace Shiny.Music;

/// <summary>
/// Represents metadata for a music track on the device.
/// </summary>
/// <param name="Id">Platform-specific unique identifier for the track. On Android this is the MediaStore row ID; on iOS it is the persistent ID.</param>
/// <param name="Title">The title of the track.</param>
/// <param name="Artist">The artist or performer of the track.</param>
/// <param name="Album">The album the track belongs to.</param>
/// <param name="Genre">The genre of the track, or <c>null</c> if not available.</param>
/// <param name="Duration">The playback duration of the track.</param>
/// <param name="AlbumArtUri">A URI pointing to the album artwork image. Available on Android via MediaStore; <c>null</c> on iOS where artwork is accessed through MPMediaItem.Artwork.</param>
/// <param name="ContentUri">
/// A URI that can be used for playback and file operations.
/// On Android this is a <c>content://</c> URI from MediaStore.
/// On iOS this is the <c>ipod-library://</c> asset URL from MPMediaItem.AssetURL.
/// This value is <see cref="string.Empty"/> for DRM-protected Apple Music subscription tracks,
/// which cannot be played via AVAudioPlayer or copied.
/// </param>
public record MusicMetadata(
    string Id,
    string Title,
    string Artist,
    string Album,
    string? Genre,
    TimeSpan Duration,
    string? AlbumArtUri,
    string ContentUri
);
