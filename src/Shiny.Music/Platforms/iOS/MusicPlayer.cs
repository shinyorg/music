using AVFoundation;
using Foundation;

namespace Shiny.Music;

public class MusicPlayer : IMusicPlayer
{
    AVPlayer? player;
    NSObject? completionObserver;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;

    public TimeSpan Position =>
        this.player?.CurrentTime is not null
            ? TimeSpan.FromSeconds(this.player.CurrentTime.Seconds)
            : TimeSpan.Zero;

    public TimeSpan Duration =>
        this.player?.CurrentItem?.Duration is not null && !double.IsNaN(this.player.CurrentItem.Duration.Seconds)
            ? TimeSpan.FromSeconds(this.player.CurrentItem.Duration.Seconds)
            : TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

        if (string.IsNullOrEmpty(track.ContentUri))
            throw new InvalidOperationException("Cannot play track: ContentUri is empty (likely DRM-protected).");

        var url = NSUrl.FromString(track.ContentUri);
        if (url == null)
            throw new InvalidOperationException($"Cannot play track: invalid content URI '{track.ContentUri}'");

        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Playback);
        audioSession.SetActive(true);

        var playerItem = new AVPlayerItem(url);
        this.player = new AVPlayer(playerItem);

        this.completionObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            AVPlayerItem.DidPlayToEndTimeNotification,
            this.OnPlaybackFinished,
            playerItem
        );

        this.player.Play();

        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);

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
            this.player.Play();
            this.SetState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        if (this.player != null)
        {
            if (this.completionObserver != null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(this.completionObserver);
                this.completionObserver.Dispose();
                this.completionObserver = null;
            }
            this.player.Pause();
            this.player.Dispose();
            this.player = null;
        }
        this.currentTrack = null;
        this.SetState(PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        this.player?.Seek(CoreMedia.CMTime.FromSeconds(position.TotalSeconds, 1000));
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

    void OnPlaybackFinished(NSNotification notification)
    {
        this.SetState(PlaybackState.Stopped);
        this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }
}
