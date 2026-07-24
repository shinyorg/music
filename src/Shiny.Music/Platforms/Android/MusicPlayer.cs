using Android.Content;
using Android.Database;
using Android.Media;
using Android.Provider;
using Shiny.Music.Internal;
using Uri = Android.Net.Uri;
using Stream = Android.Media.Stream;

namespace Shiny.Music;

public class MusicPlayer(PlayCountStore playCounts) : IMusicPlayer
{
    Android.Media.MediaPlayer? player;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;
    bool prepared;
    int audioSessionId = -1;

    // A stable audio session id reused by every MediaPlayer, so a Visualizer-based VU meter attached to it
    // keeps working across track changes (each PlayAsync creates a fresh MediaPlayer).
    internal int AudioSessionId => this.audioSessionId >= 0
        ? this.audioSessionId
        : (this.audioSessionId = this.AudioManager.GenerateAudioSessionId());

    DuckScope? activeDuck;
    CancellationTokenSource? fadeCts;
    float currentVolume = 1f;

    AudioManager? audioManager;
    VolumeObserver? volumeObserver;
    int lastVolumeStep = -1;

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;
    public bool IsDucked => this.activeDuck != null;

    // Guarded on `prepared`: while a track is still preparing (async), CurrentPosition/Duration are not
    // valid to read, so report Zero until the Prepared callback fires.
    public TimeSpan Position =>
        this.player != null && this.prepared ? TimeSpan.FromMilliseconds(this.player.CurrentPosition) : TimeSpan.Zero;

    public TimeSpan Duration =>
        this.player != null && this.prepared ? TimeSpan.FromMilliseconds(this.player.Duration) : TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    AudioManager AudioManager => this.audioManager ??=
        (AudioManager)Application.Context.GetSystemService(Context.AudioService)!;

    // Volume maps to the device-wide STREAM_MUSIC level, NOT the per-player MediaPlayer.SetVolume attenuation
    // used for ducking (currentVolume). The two are orthogonal: ducking scales this player's own output, while
    // Volume moves the system music volume that the hardware buttons control.
    public bool IsVolumeControlSupported => true;

    public float Volume
    {
        get
        {
            var max = this.AudioManager.GetStreamMaxVolume(Stream.Music);
            return max <= 0 ? 0f : this.AudioManager.GetStreamVolume(Stream.Music) / (float)max;
        }
        set
        {
            var max = this.AudioManager.GetStreamMaxVolume(Stream.Music);
            var step = (int)Math.Round(Math.Clamp(value, 0f, 1f) * max);
            // Flags 0 = change silently, without popping the system volume UI. This triggers the content observer,
            // which raises VolumeChanged.
            this.AudioManager.SetStreamVolume(Stream.Music, step, (VolumeNotificationFlags)0);
        }
    }

    event EventHandler<float>? volumeChanged;
    public event EventHandler<float>? VolumeChanged
    {
        add
        {
            this.volumeChanged += value;
            this.EnsureVolumeObserver();
        }
        remove => this.volumeChanged -= value;
    }

    // Android has no KVO; watch the system settings URI and filter to actual STREAM_MUSIC changes. Registered
    // lazily on first subscription and torn down in Dispose.
    void EnsureVolumeObserver()
    {
        if (this.volumeObserver != null)
            return;

        this.lastVolumeStep = this.AudioManager.GetStreamVolume(Stream.Music);
        this.volumeObserver = new VolumeObserver(this.OnSystemVolumeChanged);
        Application.Context.ContentResolver!.RegisterContentObserver(
            Settings.System.ContentUri!, true, this.volumeObserver);
    }

    void OnSystemVolumeChanged()
    {
        var step = this.AudioManager.GetStreamVolume(Stream.Music);
        if (step == this.lastVolumeStep)
            return;   // the observer fires for any system setting; ignore non-music-volume changes

        this.lastVolumeStep = step;
        var max = this.AudioManager.GetStreamMaxVolume(Stream.Music);
        this.volumeChanged?.Invoke(this, max <= 0 ? 0f : step / (float)max);
    }

    public Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

        var mp = new Android.Media.MediaPlayer();
        this.player = mp;
        mp.AudioSessionId = this.AudioSessionId;   // stable session so a VU Visualizer survives track changes
        mp.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        var uri = Uri.Parse(track.ContentUri)!;
        mp.SetDataSource(Application.Context, uri);
        mp.Completion += this.OnPlaybackCompleted;

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
                // player was torn down underneath us — ignore
            }
        };
        mp.PrepareAsync();

        this.currentTrack = track;
        _ = playCounts.IncrementAsync(track.Id);
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
        this.ResetDuckState();

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
        this.SetState(PlaybackState.Stopped);
    }

    public IAsyncDisposable Duck(DuckOptions? options = null)
    {
        if (this.player == null || this.state != PlaybackState.Playing)
            return DuckScope.NoOp;

        // Never layer ducks: while one is already active a second Duck() is a no-op, so there is
        // only ever one active duck (and one fade). Layering let a superseded duck's restore-fade
        // race the new duck's fade on the shared volume, which could leave the song stuck quiet.
        if (this.activeDuck != null)
            return DuckScope.NoOp;

        var opts = options ?? new DuckOptions();
        var level = (float)Math.Clamp(opts.Level, 0d, 1d);

        var scope = new DuckScope(s => this.RestoreAsync(s, opts.FadeOut));
        this.activeDuck = scope;
        _ = this.FadeToAsync(level, opts.FadeIn);
        return scope;
    }

    async ValueTask RestoreAsync(DuckScope scope, TimeSpan fadeOut)
    {
        // Last-writer-wins: only the active scope restores; a superseded scope is a no-op.
        if (this.activeDuck != scope)
            return;

        this.activeDuck = null;
        await this.FadeToAsync(1f, fadeOut).ConfigureAwait(false);
    }

    async ValueTask FadeToAsync(float target, TimeSpan duration)
    {
        this.fadeCts?.Cancel();
        var cts = new CancellationTokenSource();
        this.fadeCts = cts;
        var token = cts.Token;

        var start = this.currentVolume;
        var steps = duration <= TimeSpan.Zero ? 1 : Math.Max(1, (int)(duration.TotalMilliseconds / 15));

        try
        {
            for (var i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                var v = start + ((target - start) * (i / (float)steps));
                this.SetVolume(v);
                if (i < steps)
                    await Task.Delay(15, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer fade; leave volume where it landed.
        }
    }

    void SetVolume(float volume)
    {
        this.currentVolume = volume;
        this.player?.SetVolume(volume, volume);
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

    public IVuMeter CreateVuMeter(AudioLevels? implied = null, TimeSpan? interval = null)
    {
        var iv = interval ?? TimeSpan.FromMilliseconds(50);

        // Prefer a real output tap (Visualizer) when the app holds RECORD_AUDIO; otherwise fall back to the
        // implied meter (which needs the offline analysis).
        if (VisualizerVuMeter.HasPermission())
        {
            try
            {
                return new VisualizerVuMeter(this, this.AudioSessionId, iv);
            }
            catch
            {
                // Visualizer unavailable (missing manifest permission, device restriction) — fall back.
            }
        }

        return new SampledVuMeter(this, implied, iv);
    }

    public void Dispose()
    {
        this.Stop();

        if (this.volumeObserver != null)
        {
            Application.Context.ContentResolver!.UnregisterContentObserver(this.volumeObserver);
            this.volumeObserver.Dispose();
            this.volumeObserver = null;
        }
    }

    void SetState(PlaybackState newState)
    {
        this.state = newState;
        this.StateChanged?.Invoke(this, newState);
    }

    void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        this.ResetDuckState();   // music stopped on its own — reset the duck if one is active
        this.SetState(PlaybackState.Stopped);
        this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    // Clears any active duck and its in-flight fade, restoring the tracked volume. Idempotent —
    // safe to call when nothing is ducked.
    void ResetDuckState()
    {
        this.fadeCts?.Cancel();
        this.fadeCts = null;
        this.activeDuck = null;
        this.currentVolume = 1f;
    }

    // Fires OnChange on the main looper whenever a system setting changes; OnSystemVolumeChanged filters to
    // actual STREAM_MUSIC volume changes.
    class VolumeObserver(Action onChanged) : ContentObserver(new Android.OS.Handler(Android.OS.Looper.MainLooper!))
    {
        public override void OnChange(bool selfChange) => onChanged();
    }
}
