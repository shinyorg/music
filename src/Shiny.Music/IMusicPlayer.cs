namespace Shiny.Music;

/// <summary>
/// Controls playback of music files from the device library.
/// On Android, playback uses <c>Android.Media.MediaPlayer</c> with content URIs.
/// On iOS, playback uses <c>AVFoundation.AVAudioPlayer</c> with the track's asset URL.
/// </summary>
public interface IMusicPlayer : IDisposable
{
    /// <summary>
    /// Gets the current playback state.
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Gets the currently loaded track, or <c>null</c> if no track is loaded.
    /// </summary>
    MusicMetadata? CurrentTrack { get; }

    /// <summary>
    /// Gets the current playback position within the track. Returns <see cref="TimeSpan.Zero"/> if no track is loaded.
    /// </summary>
    TimeSpan Position { get; }

    /// <summary>
    /// Gets the total duration of the currently loaded track. Returns <see cref="TimeSpan.Zero"/> if no track is loaded.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Loads and begins playing the specified track. Any currently playing track is stopped first.
    /// </summary>
    /// <param name="track">The track to play. The <see cref="MusicMetadata.ContentUri"/> must not be empty.</param>
    /// <exception cref="InvalidOperationException">Thrown if the track cannot be loaded (e.g., empty ContentUri or playback error).</exception>
    Task PlayAsync(MusicMetadata track);

    /// <summary>
    /// Pauses the currently playing track. Has no effect if the player is not in the <see cref="PlaybackState.Playing"/> state.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes playback of a paused track. Has no effect if the player is not in the <see cref="PlaybackState.Paused"/> state.
    /// </summary>
    void Resume();

    /// <summary>
    /// Stops playback and releases the current track. The player returns to the <see cref="PlaybackState.Stopped"/> state.
    /// </summary>
    void Stop();

    /// <summary>
    /// Seeks to the specified position within the currently loaded track.
    /// </summary>
    /// <param name="position">The position to seek to.</param>
    void Seek(TimeSpan position);

    /// <summary>
    /// Raised when the playback state changes (e.g., from <see cref="PlaybackState.Playing"/> to <see cref="PlaybackState.Paused"/>).
    /// </summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// Raised when the current track finishes playing naturally (not via <see cref="Stop"/>).
    /// </summary>
    event EventHandler? PlaybackCompleted;
}
