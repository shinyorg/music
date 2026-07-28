namespace Shiny.Music;

/// <summary>
/// One playback engine behind <see cref="MusicPlayer"/>. Core ships <see cref="MediaPlayerBackend"/> for
/// local library files; additional backends (e.g. the Apple Music catalog player in
/// <c>Shiny.Music.Android.AppleMusicKit</c>) register themselves with
/// <c>TryAddEnumerable(ServiceDescriptor.Singleton&lt;IPlaybackBackend, MyBackend&gt;())</c> and are selected
/// per-track by <see cref="CanPlay"/>.
/// <para>
/// Everything the OS owns rather than the engine - system volume, output-device routing, audio focus, the
/// MediaSession and the foreground service - lives ABOVE this seam in <see cref="MusicPlayer"/>, so it is
/// written once and inherited by every backend.
/// </para>
/// <para>
/// Deliberately internal: the seam is expected to move while the Apple Music package is experimental, so it
/// is not a public API commitment. Consumers reach it via <c>InternalsVisibleTo</c>.
/// </para>
/// </summary>
interface IPlaybackBackend : IDisposable
{
    /// <summary>Whether this backend can play the given track. Backends must be mutually exclusive.</summary>
    bool CanPlay(MusicMetadata track);

    /// <summary>
    /// The audio session id this backend renders through, or <c>null</c> when it exposes none.
    /// A non-null value lets <see cref="MusicPlayer.CreateVuMeter"/> attach a real <c>Visualizer</c> output tap;
    /// <c>null</c> forces the implied meter. DRM-backed engines (Apple Music) return <c>null</c>.
    /// </summary>
    int? AudioSessionId { get; }

    /// <summary>
    /// Whether <see cref="SetAttenuation"/> is honored. When <c>false</c> the engine exposes no volume
    /// control, so <see cref="IMusicPlayer.Duck"/> cannot lower it and returns a no-op scope.
    /// </summary>
    bool IsVolumeAttenuationSupported { get; }

    PlaybackState State { get; }
    MusicMetadata? CurrentTrack { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    Task PlayAsync(MusicMetadata track);
    void Pause();
    void Resume();
    void Stop();
    void Seek(TimeSpan position);

    /// <summary>
    /// Scales this engine's own output, 0.0-1.0, without touching the device volume. Used for ducking.
    /// A no-op when <see cref="IsVolumeAttenuationSupported"/> is <c>false</c>.
    /// </summary>
    void SetAttenuation(float level);

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler? PlaybackCompleted;
}
