using Android.Content;
using Android.Database;
using Android.Media;
using Android.Provider;
using Shiny.Music.Internal;
using Stream = Android.Media.Stream;

namespace Shiny.Music;

/// <summary>
/// The Android <see cref="IMusicPlayer"/>. Routes each track to the first <see cref="IPlaybackBackend"/>
/// that reports <see cref="IPlaybackBackend.CanPlay"/>, and owns everything the OS controls rather than
/// the playback engine: system volume, audio focus, ducking, and the media-playback foreground service.
/// </summary>
public class MusicPlayer : IMusicPlayer
{
    readonly AndroidPlatform platform;
    readonly PlayCountStore playCounts;
    readonly MusicPlayerOptions options;
    readonly IReadOnlyList<IPlaybackBackend> backends;
    readonly AudioFocusManager? focus;

    IPlaybackBackend? active;
    bool serviceRunning;
    bool resumeOnFocusGain;

    // Ducking is the lower of two independent requests: one from the application (Duck()) and one from the
    // system (AUDIOFOCUS_LOSS_TRANSIENT_CAN_DUCK). Tracking them separately means neither can un-duck the
    // other - a nav prompt ending mid-announcement no longer restores full volume under an active Duck().
    float userDuck = 1f;
    float osDuck = 1f;

    DuckScope? activeDuck;
    CancellationTokenSource? fadeCts;

    AudioManager? audioManager;
    VolumeObserver? volumeObserver;
    int lastVolumeStep = -1;

    // Internal because IPlaybackBackend is: the seam is not a public API commitment while the Apple Music
    // package is experimental. AddShinyMusic registers this through a factory rather than by type, since
    // ActivatorUtilities only considers public constructors.
    internal MusicPlayer(
        AndroidPlatform platform,
        PlayCountStore playCounts,
        MusicPlayerOptions options,
        IEnumerable<IPlaybackBackend> backends
    )
    {
        this.platform = platform;
        this.playCounts = playCounts;
        this.options = options;
        this.backends = backends.ToList();

        if (this.options.RespectAudioFocus)
        {
            this.focus = new AudioFocusManager();
            this.focus.FocusChanged += this.OnFocusChanged;
        }

        foreach (var backend in this.backends)
        {
            backend.StateChanged += this.OnBackendStateChanged;
            backend.PlaybackCompleted += this.OnBackendCompleted;
        }
    }

    public PlaybackState State => this.active?.State ?? PlaybackState.Stopped;
    public MusicMetadata? CurrentTrack => this.active?.CurrentTrack;
    public TimeSpan Position => this.active?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => this.active?.Duration ?? TimeSpan.Zero;
    public bool IsDucked => this.activeDuck != null;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    /// <summary>
    /// Raised whenever the player's observable state changes in a way the MediaSession must republish
    /// (state, track, or position discontinuity). The foreground service subscribes to this to invalidate
    /// its Media3 player state.
    /// </summary>
    internal event EventHandler? SessionInvalidated;

    AudioManager AudioManager => this.audioManager ??=
        (AudioManager)Application.Context.GetSystemService(Context.AudioService)!;

    // Volume maps to the device-wide STREAM_MUSIC level, NOT the per-backend attenuation used for ducking.
    // The two are orthogonal: ducking scales the active engine's own output, while Volume moves the system
    // music volume that the hardware buttons control. Both are backend-independent, so neither regresses
    // when a track routes to a different engine.
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

    public async Task PlayAsync(MusicMetadata track)
    {
        var backend = this.backends.FirstOrDefault(x => x.CanPlay(track))
            ?? throw new InvalidOperationException(
                $"No playback backend can play track '{track.Id}'. Local tracks require a ContentUri; " +
                "streaming catalog tracks require an additional backend package to be registered.");

        // Switching engines: stop the outgoing one so two backends never render at once.
        if (this.active != null && !ReferenceEquals(this.active, backend))
            this.active.Stop();

        this.active = backend;
        this.ResetDuckState();

        // Take focus BEFORE starting, so another app's playback is stopped first rather than overlapping.
        // A denied request is not fatal - some OEM builds refuse focus for short-lived requests - so we
        // proceed rather than silently swallowing the play.
        this.focus?.Request();

        // Ask for the notification permission here — while we are demonstrably in a user-initiated,
        // foreground moment — but do NOT start the service yet. startForegroundService gives us roughly
        // five seconds to call startForeground or the OS kills the app with
        // ForegroundServiceDidNotStartInTimeException, and Media3 only posts its notification once the
        // player is actually playing. PlayAsync returns as soon as playback is *initiated* (the backend
        // prepares asynchronously), so starting the service here would race that deadline on a slow
        // prepare. The service is started from OnBackendStateChanged instead, once the state is Playing.
        if (this.options.EnableBackgroundPlayback)
            await this.platform.RequestForegroundServicePermissions().ConfigureAwait(false);

        await backend.PlayAsync(track).ConfigureAwait(false);

        _ = this.playCounts.IncrementAsync(track.Id);
        this.SessionInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        this.active?.Pause();
        this.resumeOnFocusGain = false;   // a deliberate pause outranks a pending focus-resume
    }

    public void Resume() => this.active?.Resume();

    public void Stop()
    {
        this.ResetDuckState();
        this.active?.Stop();
        this.resumeOnFocusGain = false;
        this.focus?.Abandon();
        this.StopService();
        this.SessionInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        this.active?.Seek(position);
        this.SessionInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public IAsyncDisposable Duck(DuckOptions? options = null)
    {
        if (this.active == null || this.State != PlaybackState.Playing)
            return DuckScope.NoOp;

        // An engine with no volume control (a DRM catalog player) cannot be lowered by us, and there is no
        // system mechanism to duck our own playback. Report that honestly rather than pretending.
        if (!this.active.IsVolumeAttenuationSupported)
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

        var start = this.userDuck;
        var steps = duration <= TimeSpan.Zero ? 1 : Math.Max(1, (int)(duration.TotalMilliseconds / 15));

        try
        {
            for (var i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                this.userDuck = start + ((target - start) * (i / (float)steps));
                this.ApplyAttenuation();
                if (i < steps)
                    await Task.Delay(15, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer fade; leave volume where it landed.
        }
    }

    // The effective attenuation is the lower of the application duck and the system duck, so whichever is
    // quieter wins and neither can raise the volume while the other is still active.
    void ApplyAttenuation() => this.active?.SetAttenuation(Math.Min(this.userDuck, this.osDuck));

    void OnFocusChanged(object? sender, AudioFocusEvent e)
    {
        switch (e)
        {
            case AudioFocusEvent.Lost:
                // Permanent - another app owns playback now. Stop rather than pause; there is no resume.
                this.Stop();
                break;

            case AudioFocusEvent.LostTransient:
                if (this.State == PlaybackState.Playing)
                {
                    this.resumeOnFocusGain = this.options.AutoResumeAfterInterruption;
                    this.active?.Pause();
                }
                break;

            case AudioFocusEvent.Duck:
                this.osDuck = (float)Math.Clamp(this.options.AudioFocusDuckLevel, 0d, 1d);
                this.ApplyAttenuation();
                break;

            case AudioFocusEvent.Gained:
                this.osDuck = 1f;
                this.ApplyAttenuation();
                if (this.resumeOnFocusGain)
                {
                    this.resumeOnFocusGain = false;
                    this.active?.Resume();
                }
                break;
        }
    }

    public IVuMeter CreateVuMeter(AudioLevels? implied = null, TimeSpan? interval = null)
    {
        var iv = interval ?? TimeSpan.FromMilliseconds(50);

        // Prefer a real output tap (Visualizer) when the app holds RECORD_AUDIO and the active engine
        // exposes a session id. A DRM engine exposes none, so those tracks always fall back to the implied
        // meter - and since their audio also can't be decoded offline, that meter will be silent.
        var sessionId = this.active?.AudioSessionId;
        if (sessionId != null && VisualizerVuMeter.HasPermission())
        {
            try
            {
                return new VisualizerVuMeter(this, sessionId.Value, iv);
            }
            catch
            {
                // Visualizer unavailable (missing manifest permission, device restriction) - fall back.
            }
        }

        return new SampledVuMeter(this, implied, iv);
    }

    // Started only once the backend reports Playing, so Media3 can satisfy the startForeground deadline
    // immediately (see the note in PlayAsync). Idempotent.
    void StartServiceIfNeeded()
    {
        if (this.serviceRunning || !this.options.EnableBackgroundPlayback)
            return;

        this.platform.StartService(typeof(MusicPlaybackService), true);
        this.serviceRunning = true;
    }

    void StopService()
    {
        if (!this.serviceRunning)
            return;

        this.platform.StopService(typeof(MusicPlaybackService));
        this.serviceRunning = false;
    }

    void OnBackendStateChanged(object? sender, PlaybackState state)
    {
        // Ignore chatter from an engine we've already switched away from.
        if (!ReferenceEquals(sender, this.active))
            return;

        if (state == PlaybackState.Playing)
            this.StartServiceIfNeeded();

        this.StateChanged?.Invoke(this, state);
        this.SessionInvalidated?.Invoke(this, EventArgs.Empty);
    }

    void OnBackendCompleted(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, this.active))
            return;

        this.ResetDuckState();   // music stopped on its own - reset the duck if one is active
        this.focus?.Abandon();
        this.StopService();
        this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        this.SessionInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        this.Stop();

        foreach (var backend in this.backends)
        {
            backend.StateChanged -= this.OnBackendStateChanged;
            backend.PlaybackCompleted -= this.OnBackendCompleted;
            backend.Dispose();
        }

        if (this.focus != null)
        {
            this.focus.FocusChanged -= this.OnFocusChanged;
            this.focus.Dispose();
        }

        if (this.volumeObserver != null)
        {
            Application.Context.ContentResolver!.UnregisterContentObserver(this.volumeObserver);
            this.volumeObserver.Dispose();
            this.volumeObserver = null;
        }
    }

    // Clears any active duck and its in-flight fade, restoring both duck tracks. Idempotent -
    // safe to call when nothing is ducked.
    void ResetDuckState()
    {
        this.fadeCts?.Cancel();
        this.fadeCts = null;
        this.activeDuck = null;
        this.userDuck = 1f;
        this.osDuck = 1f;
    }

    // Fires OnChange on the main looper whenever a system setting changes; OnSystemVolumeChanged filters to
    // actual STREAM_MUSIC volume changes.
    class VolumeObserver(Action onChanged) : ContentObserver(new Android.OS.Handler(Android.OS.Looper.MainLooper!))
    {
        public override void OnChange(bool selfChange) => onChanged();
    }
}
