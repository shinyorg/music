namespace Shiny.Music;

/// <summary>
/// Provides lyrics for music tracks.
/// On Android, lyrics are fetched from the LRCLIB service.
/// On iOS, lyrics are read from the local music library via <c>MPMediaItem.Lyrics</c>.
/// </summary>
public interface ILyricsProvider
{
    /// <summary>
    /// Gets lyrics for the specified track.
    /// </summary>
    /// <param name="track">The track to get lyrics for.</param>
    /// <returns>A <see cref="LyricsResult"/> containing plain and/or synced lyrics, or <c>null</c> if no lyrics are available.</returns>
    Task<LyricsResult?> GetLyricsAsync(MusicMetadata track);
}
