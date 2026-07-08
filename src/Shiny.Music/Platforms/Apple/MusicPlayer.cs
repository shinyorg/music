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
    CancellationTokenSource? deactivateCts;

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

            // We're (re)ducking: cancel a still-pending restore from a just-disposed duck so the
            // session isn't dropped and immediately re-raised between rapid announcements.
            this.deactivateCts?.Cancel();
            this.deactivateCts = null;

            // The level/fade in DuckOptions are advisory on Apple: the OS controls duck depth and ramp.
            // Anything played through the app audio session (an AVAudioPlayer announcement file, or an
            // AVSpeechSynthesizer with UsesApplicationAudioSession = true) is NOT ducked and plays over top.
            var session = AVAudioSession.SharedInstance();
            var okCategory = session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.DuckOthers, out _);
            var okActive = session.SetActive(true, out _);
            if (!okCategory || !okActive)
            {
                // Could not duck — undo any partial activation and return a no-op so the caller's flow isn't broken.
                session.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
                return DuckScope.NoOp;
            }

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
            this.BeginRestoreLocked();
        }
        return default;
    }

    // Stop()/Dispose() path — force the session back to full volume regardless of which scope owns it.
    void EndDuck()
    {
        lock (this.duckLock)
        {
            if (this.activeDuck == null && this.deactivateCts == null)
                return;

            this.activeDuck = null;
            this.BeginRestoreLocked();
        }
    }

    // Return the ducked music to full volume by deactivating the session. Caller must hold duckLock.
    void BeginRestoreLocked()
    {
        this.deactivateCts?.Cancel();
        this.deactivateCts = null;

        if (TryDeactivate())
            return;

        // SetActive(false) can fail with AVAudioSessionErrorCode.IsBusy while a just-finished
        // announcement is still draining out of the app session. Retry on a background loop until
        // it takes (or a newer duck cancels us), otherwise the song stays ducked forever.
        var cts = new CancellationTokenSource();
        this.deactivateCts = cts;
        _ = this.DeactivateRetryAsync(cts);
    }

    static bool TryDeactivate()
        => AVAudioSession
            .SharedInstance()
            .SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);

    async Task DeactivateRetryAsync(CancellationTokenSource cts)
    {
        var ct = cts.Token;
        try
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);

                lock (this.duckLock)
                {
                    // A newer duck (re-activation) or restore superseded this attempt — stop.
                    if (ct.IsCancellationRequested || this.deactivateCts != cts)
                        return;

                    if (TryDeactivate())
                    {
                        this.deactivateCts = null;
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Never leave the field pointing at a disposed source (retries exhausted or cancelled),
            // or the next Duck()/EndDuck() would Cancel() a disposed CTS and throw.
            lock (this.duckLock)
            {
                if (this.deactivateCts == cts)
                    this.deactivateCts = null;
            }
            cts.Dispose();
        }
    }

    public void Seek(TimeSpan position)
    {
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
