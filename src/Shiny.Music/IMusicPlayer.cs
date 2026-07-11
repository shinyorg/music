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

    /// <summary>
    /// Gets a value indicating whether setting <see cref="Volume"/> is supported on the current platform.
    /// <c>true</c> on Android; <c>false</c> on Apple platforms, where the OS exposes no supported API to change the
    /// system volume. Reading <see cref="Volume"/> and observing <see cref="VolumeChanged"/> work on all platforms.
    /// </summary>
    bool IsVolumeControlSupported { get; }

    /// <summary>
    /// Gets or sets the device media volume, normalized 0.0–1.0. This is the system music-stream volume (device-wide),
    /// independent of any active duck.
    /// </summary>
    /// <remarks>
    /// Reading works on all platforms. Setting is only supported where <see cref="IsVolumeControlSupported"/> is
    /// <c>true</c>. On Android the underlying stream is integer-stepped, so a set value is quantized to the nearest step
    /// and reading it back may differ slightly. On Apple platforms the setter throws <see cref="NotSupportedException"/>,
    /// as iOS and Mac Catalyst provide no supported API to change the system volume — let the user adjust it with the
    /// hardware buttons or an <c>MPVolumeView</c>.
    /// </remarks>
    /// <exception cref="NotSupportedException">Thrown by the setter on Apple platforms; guard with <see cref="IsVolumeControlSupported"/>.</exception>
    float Volume { get; set; }

    /// <summary>
    /// Raised when the device media volume changes — via the hardware buttons, Control Center, or a successful
    /// <see cref="Volume"/> set. The argument is the new volume normalized 0.0–1.0.
    /// </summary>
    event EventHandler<float>? VolumeChanged;

    /// <summary>
    /// Gets a value indicating whether a duck scope is currently active.
    /// </summary>
    bool IsDucked { get; }

    /// <summary>
    /// Lowers the currently playing music so an announcement can be heard over top, until the returned scope is disposed.
    /// Uses last-writer-wins semantics: a newer duck supersedes an older one; disposing the active scope restores full
    /// volume, and disposing a superseded scope has no effect. Returns a no-op scope if nothing is currently playing.
    /// </summary>
    /// <remarks>
    /// On Android this lowers this player's own track, honoring <see cref="DuckOptions.Level"/> and the fade durations.
    /// On Apple this activates <c>AVAudioSession</c> with <c>DuckOthers</c>; the level and fades are advisory only, as the
    /// OS controls duck depth and ramp. Audio played through the app audio session (an <c>AVAudioPlayer</c> announcement
    /// file, or <c>AVSpeechSynthesizer</c> with <c>UsesApplicationAudioSession = true</c>) is not ducked and plays at full
    /// volume over the ducked music; only other out-of-process audio is ducked.
    /// </remarks>
    /// <param name="options">Duck level and fade durations. When <c>null</c>, the <see cref="DuckOptions"/> defaults are used.</param>
    /// <returns>A scope that restores the prior volume when disposed.</returns>
    IAsyncDisposable Duck(DuckOptions? options = null);
}
