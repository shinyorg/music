namespace Shiny.Music;

/// <summary>
/// Identifies songs by listening to audio through the device microphone.
/// On iOS this uses ShazamKit; Android does not currently have an implementation.
/// </summary>
public interface IMusicIdentifier
{
    /// <summary>
    /// Begins listening through the device microphone and attempts to identify the currently playing song.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to stop listening.</param>
    /// <returns>A <see cref="MusicIdentificationResult"/> if a song is identified, or <c>null</c> if no match is found.</returns>
    Task<MusicIdentificationResult?> ListenAsync(CancellationToken cancellationToken = default);
}
