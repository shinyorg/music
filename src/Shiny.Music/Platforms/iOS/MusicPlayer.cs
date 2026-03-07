using AVFoundation;
using Foundation;
using MediaPlayer;

namespace Shiny.Music;

enum ActivePlayerKind { None, AVPlayer, SystemMusicPlayer }

public class MusicPlayer : IMusicPlayer
{
    AVPlayer? avPlayer;
    NSObject? completionObserver;
    NSObject? stateObserver;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;
    ActivePlayerKind activeKind = ActivePlayerKind.None;
    bool explicitStop;

    MPMusicPlayerController AppPlayer => MPMusicPlayerController.ApplicationMusicPlayer;

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;

    public TimeSpan Position => this.activeKind switch
    {
        ActivePlayerKind.AVPlayer when this.avPlayer?.CurrentTime is not null =>
            TimeSpan.FromSeconds(this.avPlayer.CurrentTime.Seconds),
        ActivePlayerKind.SystemMusicPlayer =>
            TimeSpan.FromSeconds(this.AppPlayer.CurrentPlaybackTime),
        _ => TimeSpan.Zero
    };

    public TimeSpan Duration => this.activeKind switch
    {
        ActivePlayerKind.AVPlayer when this.avPlayer?.CurrentItem?.Duration is not null
            && !double.IsNaN(this.avPlayer.CurrentItem.Duration.Seconds) =>
            TimeSpan.FromSeconds(this.avPlayer.CurrentItem.Duration.Seconds),
        ActivePlayerKind.SystemMusicPlayer =>
            TimeSpan.FromSeconds(this.AppPlayer.NowPlayingItem?.PlaybackDuration ?? 0),
        _ => TimeSpan.Zero
    };

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public async Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

        if (!string.IsNullOrEmpty(track.ContentUri))
        {
            this.PlayViaAVPlayer(track);
        }
        else if (!string.IsNullOrEmpty(track.StoreId))
        {
            await this.PlayViaAppPlayerAsync(track);
        }
        else
        {
            throw new InvalidOperationException("Cannot play track: both ContentUri and StoreId are empty.");
        }
    }

    void PlayViaAVPlayer(MusicMetadata track)
    {
        var url = NSUrl.FromString(track.ContentUri);
        if (url == null)
            throw new InvalidOperationException($"Cannot play track: invalid content URI '{track.ContentUri}'");

        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Playback);
        audioSession.SetActive(true);

        var playerItem = new AVPlayerItem(url);
        this.avPlayer = new AVPlayer(playerItem);

        this.completionObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            AVPlayerItem.DidPlayToEndTimeNotification,
            this.OnAVPlaybackFinished,
            playerItem
        );

        this.avPlayer.Play();
        this.activeKind = ActivePlayerKind.AVPlayer;
        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);
    }

    async Task PlayViaAppPlayerAsync(MusicMetadata track)
    {
        // Look up the MPMediaItem by persistent ID for reliable queue setting
        var query = new MPMediaQuery();
        query.AddFilterPredicate(
            MPMediaPropertyPredicate.PredicateWithValue(
                NSNumber.FromUInt64(ulong.Parse(track.Id)),
                MPMediaItem.PersistentIDProperty
            )
        );

        var items = query.Items;
        if (items == null || items.Length == 0)
            throw new InvalidOperationException("Track not found in music library.");

        var collection = new MPMediaItemCollection(items);
        this.AppPlayer.SetQueue(collection);
        await this.AppPlayer.PrepareToPlayAsync();
        this.AppPlayer.Play();

        this.explicitStop = false;
        this.activeKind = ActivePlayerKind.SystemMusicPlayer;
        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);

        this.AppPlayer.BeginGeneratingPlaybackNotifications();
        this.stateObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            MPMusicPlayerController.PlaybackStateDidChangeNotification,
            this.OnSystemPlayerStateChanged,
            this.AppPlayer
        );
    }

    public void Pause()
    {
        if (this.state != PlaybackState.Playing)
            return;

        switch (this.activeKind)
        {
            case ActivePlayerKind.AVPlayer:
                this.avPlayer?.Pause();
                break;
            case ActivePlayerKind.SystemMusicPlayer:
                this.AppPlayer.Pause();
                break;
        }
        this.SetState(PlaybackState.Paused);
    }

    public void Resume()
    {
        if (this.state != PlaybackState.Paused)
            return;

        switch (this.activeKind)
        {
            case ActivePlayerKind.AVPlayer:
                this.avPlayer?.Play();
                break;
            case ActivePlayerKind.SystemMusicPlayer:
                this.AppPlayer.Play();
                break;
        }
        this.SetState(PlaybackState.Playing);
    }

    public void Stop()
    {
        this.explicitStop = true;

        switch (this.activeKind)
        {
            case ActivePlayerKind.AVPlayer:
                this.StopAVPlayer();
                break;
            case ActivePlayerKind.SystemMusicPlayer:
                this.StopSystemPlayer();
                break;
        }

        this.activeKind = ActivePlayerKind.None;
        this.currentTrack = null;
        this.SetState(PlaybackState.Stopped);
    }

    void StopAVPlayer()
    {
        if (this.avPlayer == null)
            return;

        if (this.completionObserver != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(this.completionObserver);
            this.completionObserver.Dispose();
            this.completionObserver = null;
        }
        this.avPlayer.Pause();
        this.avPlayer.Dispose();
        this.avPlayer = null;
    }

    void StopSystemPlayer()
    {
        this.AppPlayer.Stop();

        if (this.stateObserver != null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(this.stateObserver);
            this.stateObserver.Dispose();
            this.stateObserver = null;
        }
        this.AppPlayer.EndGeneratingPlaybackNotifications();
    }

    public void Seek(TimeSpan position)
    {
        switch (this.activeKind)
        {
            case ActivePlayerKind.AVPlayer:
                this.avPlayer?.Seek(CoreMedia.CMTime.FromSeconds(position.TotalSeconds, 1000));
                break;
            case ActivePlayerKind.SystemMusicPlayer:
                this.AppPlayer.CurrentPlaybackTime = position.TotalSeconds;
                break;
        }
    }

    public void Dispose()
    {
        this.Stop();
        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetActive(false);
    }

    void SetState(PlaybackState newState)
    {
        this.state = newState;
        this.StateChanged?.Invoke(this, newState);
    }

    void OnAVPlaybackFinished(NSNotification notification)
    {
        this.SetState(PlaybackState.Stopped);
        this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    void OnSystemPlayerStateChanged(NSNotification notification)
    {
        var mpState = this.AppPlayer.PlaybackState;

        switch (mpState)
        {
            case MPMusicPlaybackState.Playing:
                this.SetState(PlaybackState.Playing);
                break;

            case MPMusicPlaybackState.Paused:
                this.SetState(PlaybackState.Paused);
                break;

            case MPMusicPlaybackState.Stopped:
                if (!this.explicitStop && this.state != PlaybackState.Stopped)
                {
                    this.SetState(PlaybackState.Stopped);
                    this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                }
                break;

            case MPMusicPlaybackState.Interrupted:
                this.SetState(PlaybackState.Paused);
                break;
        }
    }
}
