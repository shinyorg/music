using AVFoundation;
using Foundation;
using MediaPlayer;

namespace Shiny.Music;

public class MusicPlayer : IMusicPlayer
{
    readonly MPMusicPlayerController player = MPMusicPlayerController.ApplicationMusicPlayer;
    NSObject? stateObserver;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;
    bool explicitStop;

    readonly object duckLock = new();
    DuckScope? activeDuck;
    bool sessionActive;

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;
    public bool IsDucked => this.activeDuck != null;

    public TimeSpan Position =>
        this.state != PlaybackState.Stopped
            ? TimeSpan.FromSeconds(this.player.CurrentPlaybackTime)
            : TimeSpan.Zero;

    public TimeSpan Duration => this.currentTrack?.Duration ?? TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

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

        this.explicitStop = false;
        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);
        this.StartObserving();

        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (this.state != PlaybackState.Playing)
            return;

        this.player.Pause();
        this.SetState(PlaybackState.Paused);
    }

    public void Resume()
    {
        if (this.state != PlaybackState.Paused)
            return;

        this.player.Play();
        this.SetState(PlaybackState.Playing);
    }

    public void Stop()
    {
        this.explicitStop = true;
        this.StopObserving();
        this.EndDuck();
        this.player.Stop();
        this.currentTrack = null;
        this.SetState(PlaybackState.Stopped);
    }

    public IAsyncDisposable Duck(DuckOptions? options = null)
    {
        if (this.state != PlaybackState.Playing)
            return DuckScope.NoOp;

        lock (this.duckLock)
        {
            // Never layer ducks: while one is already active a second Duck() is a no-op, so there is
            // only ever one active duck (and one restore). Layering let a superseded duck's restore
            // fight the new duck, which could leave the song stuck at a lowered level.
            if (this.activeDuck != null)
                return DuckScope.NoOp;

            // Duck by activating our session with DuckOthers. The level/fade in DuckOptions are
            // advisory on Apple: the OS controls duck depth and ramp. Anything played through the app
            // audio session (an AVAudioPlayer announcement file, or an AVSpeechSynthesizer with
            // UsesApplicationAudioSession = true) is NOT ducked and plays over top.
            if (!this.ApplyCategoryLocked(AVAudioSessionCategoryOptions.DuckOthers))
                return DuckScope.NoOp;

            var scope = new DuckScope(this.RestoreAsync);
            this.activeDuck = scope;
            return scope;
        }
    }

    ValueTask RestoreAsync(DuckScope scope)
    {
        lock (this.duckLock)
        {
            // Only the active scope restores; a superseded scope is a no-op.
            if (this.activeDuck != scope)
                return default;

            this.activeDuck = null;

            // Un-duck WITHOUT deactivating the session — just switch it to MixWithOthers so the music
            // returns to full while we stay active. SetActive(false) fails with IsBusy while an
            // announcement is still draining, and during a team walkout's rapid roll-call the session
            // is essentially never idle, so deactivation kept failing and the music stayed ducked the
            // whole time. Re-applying the category has no such failure mode, so the music reliably
            // swells back between names. The session is fully released later, in EndDuck().
            this.ApplyCategoryLocked(AVAudioSessionCategoryOptions.MixWithOthers);
        }
        return default;
    }

    // Stop()/Dispose() path — release the session so the ducked music (and everything else) returns
    // to normal. Called when the game is stopping, so the app audio is idle and deactivation succeeds.
    void EndDuck()
    {
        lock (this.duckLock)
        {
            this.activeDuck = null;
            if (!this.sessionActive)
                return;

            AVAudioSession
                .SharedInstance()
                .SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
            this.sessionActive = false;
        }
    }

    // Apply the given options to an active Playback session. Toggling DuckOthers <-> MixWithOthers this
    // way changes the duck state on the (out-of-process) music without the SetActive(false) IsBusy
    // problem. Caller must hold duckLock. Returns false if the session could not be activated.
    bool ApplyCategoryLocked(AVAudioSessionCategoryOptions options)
    {
        var session = AVAudioSession.SharedInstance();
        var okCategory = session.SetCategory(AVAudioSessionCategory.Playback, options, out _);
        var okActive = session.SetActive(true, out _);
        this.sessionActive = okActive;
        return okCategory && okActive;
    }

    public void Seek(TimeSpan position)
    {
        // Setting CurrentPlaybackTime on MPMusicPlayerController resumes playback, so a Seek that
        // races in just after Stop() (e.g. a slice-loop's final poll) would resurrect stopped music.
        // Ignore seeks once stopped so Stop() reliably stays stopped.
        if (this.state == PlaybackState.Stopped)
            return;

        this.player.CurrentPlaybackTime = position.TotalSeconds;
    }

    public void Dispose()
    {
        this.Stop();
    }

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
            this.OnPlaybackStateChanged);
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

    void OnPlaybackStateChanged(NSNotification notification)
    {
        if (this.explicitStop)
            return;

        var mpState = this.player.PlaybackState;
        switch (mpState)
        {
            case MPMusicPlaybackState.Playing when this.state != PlaybackState.Playing:
                this.SetState(PlaybackState.Playing);
                break;

            case MPMusicPlaybackState.Paused when this.state != PlaybackState.Paused:
                this.SetState(PlaybackState.Paused);
                break;

            case MPMusicPlaybackState.Stopped when this.state == PlaybackState.Playing:
                this.SetState(PlaybackState.Stopped);
                this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                this.StopObserving();
                break;

            case MPMusicPlaybackState.Interrupted:
                this.SetState(PlaybackState.Paused);
                break;
        }
    }
}
