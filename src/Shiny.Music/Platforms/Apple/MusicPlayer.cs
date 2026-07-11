using System.Runtime.ExceptionServices;
using AVFoundation;
using CoreFoundation;
using Foundation;
using MediaPlayer;

namespace Shiny.Music;

public class MusicPlayer : IMusicPlayer
{
    // Temporary diagnostics — writes to the iOS device console. Remove once stop behavior is confirmed.
    static void Log(string message) => Console.WriteLine($"[MusicPlayer] {message}");

    // MPMusicPlayerController and AVAudioSession are main-thread-affine, and this player is driven
    // from two threads: the app/UI thread (Play/Stop/Duck/dispose + the OS playback-state observer)
    // and a background slice-loop that polls Position/State and calls Seek/Stop. To make that safe we
    // funnel EVERY native call and all shared-state access through the main thread via RunOnMain, so
    // there is only ever one thread touching the player and its fields at a time. Without this the
    // ducking and Stop() behaved non-deterministically under a team walkout's concurrent load.
    readonly MPMusicPlayerController player = MPMusicPlayerController.ApplicationMusicPlayer;
    NSObject? stateObserver;
    IDisposable? volumeObserver;
    MusicMetadata? currentTrack;
    volatile PlaybackState state = PlaybackState.Stopped;
    bool explicitStop;
    bool observedPlaying;   // has the OS reported this track actually Playing since the last PlayCore?

    DuckScope? activeDuck;
    bool sessionActive;

    // A plain field read (no native call); volatile keeps it visible to the background poll loop.
    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;
    public bool IsDucked => this.activeDuck != null;

    public TimeSpan Position => RunOnMain(() =>
        this.state != PlaybackState.Stopped
            ? TimeSpan.FromSeconds(this.player.CurrentPlaybackTime)
            : TimeSpan.Zero);

    public TimeSpan Duration => this.currentTrack?.Duration ?? TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    // Apple exposes no supported API to set the system volume: MPMusicPlayerController.Volume was deprecated in
    // iOS 7 and is a no-op, and AVAudioSession.OutputVolume is read-only. So reading works, setting does not.
    public bool IsVolumeControlSupported => false;

    public float Volume
    {
        // OutputVolume is the reliable system-volume read; it reflects the current output route (same value the
        // volume HUD shows). Requires the session to be active, which the VolumeChanged observer ensures.
        get => RunOnMain(() => AVAudioSession.SharedInstance().OutputVolume);
        set => throw new NotSupportedException(
            "Setting the system volume is not supported on Apple platforms. Check IMusicPlayer.IsVolumeControlSupported before setting, and let the user adjust volume with the hardware buttons or an MPVolumeView.");
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

    // KVO on AVAudioSession.outputVolume — fires for hardware buttons and Control Center. OutputVolume only
    // updates while the session is active, so activate it (MixWithOthers, so we don't disturb other audio) if it
    // isn't already. Registered lazily on first subscription and disposed in Dispose.
    void EnsureVolumeObserver() => RunOnMain(() =>
    {
        if (this.volumeObserver != null)
            return;

        var session = AVAudioSession.SharedInstance();
        if (!this.sessionActive)
            this.ApplyCategory(AVAudioSessionCategoryOptions.MixWithOthers);

        this.volumeObserver = session.AddObserver("outputVolume", NSKeyValueObservingOptions.New, change =>
        {
            var volume = (change.NewValue as NSNumber)?.FloatValue ?? session.OutputVolume;
            this.volumeChanged?.Invoke(this, volume);
        });
    });

    public Task PlayAsync(MusicMetadata track)
    {
        RunOnMain(() => this.PlayCore(track));
        return Task.CompletedTask;
    }

    void PlayCore(MusicMetadata track)
    {
        Log($"PlayCore ENTER main={NSThread.Current.IsMainThread} id={track.Id} title={track.Title} playerState={this.player.PlaybackState}");
        this.StopCore();
        this.explicitStop = false;    // clear the stop intent before issuing the new play
        this.observedPlaying = false; // this track hasn't actually started yet

        if (!string.IsNullOrEmpty(track.CatalogId))
        {
            // Streaming catalog track (from SearchCatalogAsync) — enqueue by catalog id.
            // Requires an active Apple Music subscription; the item need not be in the local library.
            this.player.SetQueue(new MPMusicPlayerStoreQueueDescriptor(new[] { track.CatalogId }));
            this.player.Play();
        }
        else
        {
            if (!ulong.TryParse(track.Id, out var pid))
                throw new InvalidOperationException("Invalid track ID.");

            var query = MPMediaQuery.SongsQuery;
            var item = query.Items?.FirstOrDefault(i => i.PersistentID == pid)
                ?? throw new InvalidOperationException("Track not found in music library.");

            this.player.SetQueue(new MPMediaItemCollection(new[] { item }));
            this.player.Play();
        }

        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);
        this.StartObserving();
    }

    public void Pause() => RunOnMain(() =>
    {
        if (this.state != PlaybackState.Playing)
            return;

        this.player.Pause();
        this.SetState(PlaybackState.Paused);
    });

    public void Resume() => RunOnMain(() =>
    {
        if (this.state != PlaybackState.Paused)
            return;

        this.player.Play();
        this.SetState(PlaybackState.Playing);
    });

    public void Stop() => RunOnMain(this.StopCore);

    void StopCore()
    {
        Log($"StopCore ENTER main={NSThread.Current.IsMainThread} state={this.state} playerState={this.player.PlaybackState}");
        this.explicitStop = true;
        this.EndDuckCore();
        this.player.Stop();
        this.currentTrack = null;
        this.SetState(PlaybackState.Stopped);
        Log($"StopCore EXIT playerState={this.player.PlaybackState}");

        // Deliberately keep observing after a Stop(). SetQueue/Play on MPMusicPlayerController is
        // asynchronous, so a play() issued a moment earlier can still fire AFTER this Stop() and
        // resurrect the track — which is why stopping a song within a second or two of starting it
        // "didn't work". OnPlaybackStateChanged catches that stray Playing state and forces it back
        // down. Observing is reset by the next PlayCore and torn down in Dispose.
    }

    public IAsyncDisposable Duck(DuckOptions? options = null) => RunOnMain<IAsyncDisposable>(() =>
    {
        if (this.state != PlaybackState.Playing)
            return DuckScope.NoOp;

        // Never layer ducks: while one is already active a second Duck() is a no-op, so there is
        // only ever one active duck (and one restore). Layering let a superseded duck's restore
        // fight the new duck, which could leave the song stuck at a lowered level.
        if (this.activeDuck != null)
            return DuckScope.NoOp;

        // Duck by activating our session with DuckOthers. The level/fade in DuckOptions are
        // advisory on Apple: the OS controls duck depth and ramp. Anything played through the app
        // audio session (an AVAudioPlayer announcement file, or an AVSpeechSynthesizer with
        // UsesApplicationAudioSession = true) is NOT ducked and plays over top.
        if (!this.ApplyCategory(AVAudioSessionCategoryOptions.DuckOthers))
            return DuckScope.NoOp;

        var scope = new DuckScope(this.RestoreAsync);
        this.activeDuck = scope;
        return scope;
    });

    ValueTask RestoreAsync(DuckScope scope)
    {
        RunOnMain(() =>
        {
            // Only the active scope restores; a superseded scope is a no-op.
            if (this.activeDuck != scope)
                return;

            this.activeDuck = null;

            // Un-duck WITHOUT deactivating the session — just switch it to MixWithOthers so the music
            // returns to full while we stay active. SetActive(false) fails with IsBusy while an
            // announcement is still draining, and during a team walkout's rapid roll-call the session
            // is essentially never idle, so deactivation kept failing and the music stayed ducked the
            // whole time. Re-applying the category has no such failure mode, so the music reliably
            // swells back between names. The session is fully released later, in Stop()/EndDuck().
            this.ApplyCategory(AVAudioSessionCategoryOptions.MixWithOthers);
        });
        return default;
    }

    // Release the session so the ducked music (and everything else) returns to normal. Runs as part of
    // StopCore (already on the main thread), when the app audio is idle and deactivation succeeds.
    void EndDuckCore()
    {
        this.activeDuck = null;
        if (!this.sessionActive)
            return;

        AVAudioSession
            .SharedInstance()
            .SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
        this.sessionActive = false;
    }

    // Apply the given options to an active Playback session. Toggling DuckOthers <-> MixWithOthers this
    // way changes the duck state on the (out-of-process) music without the SetActive(false) IsBusy
    // problem. Runs on the main thread. Returns false if the session could not be activated.
    bool ApplyCategory(AVAudioSessionCategoryOptions options)
    {
        var session = AVAudioSession.SharedInstance();
        var okCategory = session.SetCategory(AVAudioSessionCategory.Playback, options, out _);
        var okActive = session.SetActive(true, out _);
        this.sessionActive = okActive;
        return okCategory && okActive;
    }

    public void Seek(TimeSpan position) => RunOnMain(() =>
    {
        // Setting CurrentPlaybackTime on MPMusicPlayerController resumes playback, so a Seek that
        // races in just after Stop() (e.g. a slice-loop's final poll) would resurrect stopped music.
        // Ignore seeks once stopped so Stop() reliably stays stopped.
        if (this.state == PlaybackState.Stopped)
        {
            Log($"Seek IGNORED (stopped) pos={position.TotalSeconds}s");
            return;
        }

        Log($"Seek pos={position.TotalSeconds}s main={NSThread.Current.IsMainThread}");
        this.player.CurrentPlaybackTime = position.TotalSeconds;
    });

    public void Dispose() => RunOnMain(() =>
    {
        this.StopCore();
        this.StopObserving();
        this.volumeObserver?.Dispose();   // removes the KVO registration
        this.volumeObserver = null;
    });

    void SetState(PlaybackState newState)
    {
        this.state = newState;
        this.StateChanged?.Invoke(this, newState);
    }

    void StartObserving()
    {
        this.StopObserving();
        this.stateObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            MPMusicPlayerController.PlaybackStateDidChangeNotification,
            _ => this.OnPlaybackStateChanged());
        this.player.BeginGeneratingPlaybackNotifications();
    }

    void StopObserving()
    {
        if (this.stateObserver != null)
        {
            this.player.EndGeneratingPlaybackNotifications();
            NSNotificationCenter.DefaultCenter.RemoveObserver(this.stateObserver);
            this.stateObserver = null;
        }
    }

    // The OS delivers this notification on the main thread, but funnel through RunOnMain anyway so it
    // is serialized with every other player operation regardless of delivery thread.
    void OnPlaybackStateChanged() => RunOnMain(() =>
    {
        if (this.explicitStop)
        {
            // A queued play() from a just-started track can fire after Stop() (async SetQueue/Play);
            // force it back down so a Stop within a second or two of starting the track still wins.
            // Re-stopping settles to Stopped, at which point this branch becomes a no-op.
            Log($"Observer while explicitStop — playerState={this.player.PlaybackState} (forcing stop if not Stopped)");
            if (this.player.PlaybackState != MPMusicPlaybackState.Stopped)
                this.player.Stop();
            return;
        }

        var mpState = this.player.PlaybackState;
        Log($"Observer mpState={mpState} state={this.state} observedPlaying={this.observedPlaying}");

        // The track has genuinely started once the OS reports Playing. Until then, a transient
        // Stopped is just the async SetQueue/Play spinning up — NOT a completion.
        if (mpState == MPMusicPlaybackState.Playing)
            this.observedPlaying = true;

        switch (mpState)
        {
            case MPMusicPlaybackState.Playing when this.state != PlaybackState.Playing:
                this.SetState(PlaybackState.Playing);
                break;

            case MPMusicPlaybackState.Paused when this.state != PlaybackState.Paused:
                this.SetState(PlaybackState.Paused);
                break;

            // Only a real end-of-track: we must have seen it actually playing first. This stops a
            // load-time transient Stopped from faking a completion and (critically) tearing down the
            // observer — which would leave a late play() with nothing to re-stop it.
            case MPMusicPlaybackState.Stopped when this.state == PlaybackState.Playing && this.observedPlaying:
                this.EndDuckCore();   // music stopped on its own — reset the duck if one is active
                this.SetState(PlaybackState.Stopped);
                this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                this.StopObserving();
                break;

            case MPMusicPlaybackState.Interrupted:
                this.SetState(PlaybackState.Paused);
                break;
        }
    });

    // Run an action on the main thread and wait for it. Executes inline when already on the main
    // thread; otherwise dispatches synchronously so the caller (e.g. the background slice-loop) blocks
    // until it completes. Exceptions are marshalled back to the caller with their stack intact.
    static void RunOnMain(Action action)
    {
        if (NSThread.Current.IsMainThread)
        {
            action();
            return;
        }

        ExceptionDispatchInfo? captured = null;
        DispatchQueue.MainQueue.DispatchSync(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ExceptionDispatchInfo.Capture(ex); }
        });
        captured?.Throw();
    }

    static T RunOnMain<T>(Func<T> func)
    {
        if (NSThread.Current.IsMainThread)
            return func();

        var result = default(T)!;
        ExceptionDispatchInfo? captured = null;
        DispatchQueue.MainQueue.DispatchSync(() =>
        {
            try { result = func(); }
            catch (Exception ex) { captured = ExceptionDispatchInfo.Capture(ex); }
        });
        captured?.Throw();
        return result;
    }
}
