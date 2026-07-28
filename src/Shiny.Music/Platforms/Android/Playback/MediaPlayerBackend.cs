using Android.Content;
using Android.Media;
using Android.OS;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

/// <summary>
/// The core backend: plays local library files through <c>Android.Media.MediaPlayer</c> using the track's
/// content URI. Lifted verbatim from the pre-backend <c>MusicPlayer</c> apart from the wake mode, which is
/// new - without <c>SetWakeMode</c> the CPU can suspend with the screen off and playback stalls.
/// </summary>
class MediaPlayerBackend : IPlaybackBackend
{
    Android.Media.MediaPlayer? player;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;
    bool prepared;
    int audioSessionId = -1;
    float attenuation = 1f;

    AudioManager? audioManager;
    AudioManager AudioManager => this.audioManager ??=
        (AudioManager)Application.Context.GetSystemService(Context.AudioService)!;

    // A stable audio session id reused by every MediaPlayer, so a Visualizer-based VU meter attached to it
    // keeps working across track changes (each PlayAsync creates a fresh MediaPlayer).
    public int? AudioSessionId => this.audioSessionId >= 0
        ? this.audioSessionId
        : (this.audioSessionId = this.AudioManager.GenerateAudioSessionId());

    public bool IsVolumeAttenuationSupported => true;
    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    // Local files always carry a content URI; catalog/streaming tracks do not and belong to another backend.
    public bool CanPlay(MusicMetadata track) => !string.IsNullOrEmpty(track.ContentUri);

    // Guarded on `prepared`: while a track is still preparing (async), CurrentPosition/Duration are not
    // valid to read, so report Zero until the Prepared callback fires.
    public TimeSpan Position =>
        this.player != null && this.prepared ? TimeSpan.FromMilliseconds(this.player.CurrentPosition) : TimeSpan.Zero;

    public TimeSpan Duration =>
        this.player != null && this.prepared ? TimeSpan.FromMilliseconds(this.player.Duration) : TimeSpan.Zero;

    public Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

        var mp = new Android.Media.MediaPlayer();
        this.player = mp;
        mp.AudioSessionId = this.AudioSessionId!.Value;   // stable session so a VU Visualizer survives track changes
        mp.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        // Hold a partial wake lock for the duration of playback. Without this the CPU is free to suspend
        // once the screen goes off, which stalls or stutters the decode - the single most common cause of
        // "it stops playing in my pocket".
        mp.SetWakeMode(Application.Context, WakeLockFlags.Partial);

        var uri = Uri.Parse(track.ContentUri)!;
        mp.SetDataSource(Application.Context, uri);
        mp.Completion += this.OnPlaybackCompleted;

        // Re-apply any duck that is active across the track change, so a new track doesn't start at full
        // volume in the middle of an announcement.
        mp.SetVolume(this.attenuation, this.attenuation);

        // Prepare asynchronously so the call returns as soon as playback is *initiated*, rather than
        // blocking the caller thread until the track is buffered/ready (which the synchronous Prepare()
        // did). This matches the Apple implementation's fire-and-forget behaviour. Playback starts from
        // the Prepared callback, guarded against a Stop()/new PlayAsync that swapped the player first.
        mp.Prepared += (_, _) =>
        {
            if (!ReferenceEquals(this.player, mp))
                return;   // superseded before preparation completed
            try
            {
                this.prepared = true;
                mp.Start();
                this.SetState(PlaybackState.Playing);
            }
            catch (Java.Lang.IllegalStateException)
            {
                // player was torn down underneath us - ignore
            }
        };
        mp.PrepareAsync();

        this.currentTrack = track;
        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (this.player != null && this.state == PlaybackState.Playing)
        {
            this.player.Pause();
            this.SetState(PlaybackState.Paused);
        }
    }

    public void Resume()
    {
        if (this.player != null && this.state == PlaybackState.Paused)
        {
            this.player.Start();
            this.SetState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        if (this.player != null)
        {
            this.player.Completion -= this.OnPlaybackCompleted;
            if (this.prepared && this.player.IsPlaying)
                this.player.Stop();
            this.player.Reset();
            this.player.Release();
            this.player = null;
        }
        this.prepared = false;
        this.currentTrack = null;
        this.attenuation = 1f;
        this.SetState(PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        // Ignore seeks once stopped: a slice-loop's final poll can race in just after Stop(), and
        // seeking a stopped/released MediaPlayer throws IllegalStateException (or could revive it).
        // Also ignore before the track has finished preparing (seeking an unprepared player throws).
        if (this.state == PlaybackState.Stopped || !this.prepared)
            return;

        this.player?.SeekTo((int)position.TotalMilliseconds);
    }

    public void SetAttenuation(float level)
    {
        this.attenuation = level;
        this.player?.SetVolume(level, level);
    }

    public void Dispose() => this.Stop();

    void SetState(PlaybackState newState)
    {
        this.state = newState;
        this.StateChanged?.Invoke(this, newState);
    }

    void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        this.SetState(PlaybackState.Stopped);
        this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }
}
