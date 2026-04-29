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

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;

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

        if (!ulong.TryParse(track.Id, out var pid))
            throw new InvalidOperationException("Invalid track ID.");

        var query = MPMediaQuery.SongsQuery;
        var item = query.Items?.FirstOrDefault(i => i.PersistentID == pid)
            ?? throw new InvalidOperationException("Track not found in music library.");

        this.player.SetQueue(new MPMediaItemCollection(new[] { item }));
        this.player.Play();

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
        this.player.Stop();
        this.currentTrack = null;
        this.SetState(PlaybackState.Stopped);
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
