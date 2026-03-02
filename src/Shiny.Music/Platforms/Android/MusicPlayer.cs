using Android.Media;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

public class MusicPlayer : IMusicPlayer
{
    Android.Media.MediaPlayer? player;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;

    public TimeSpan Position =>
        this.player != null ? TimeSpan.FromMilliseconds(this.player.CurrentPosition) : TimeSpan.Zero;

    public TimeSpan Duration =>
        this.player != null ? TimeSpan.FromMilliseconds(this.player.Duration) : TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current activity available");

        this.player = new Android.Media.MediaPlayer();
        this.player.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        var uri = Uri.Parse(track.ContentUri)!;
        this.player.SetDataSource(activity, uri);
        this.player.Prepare();
        this.player.Start();

        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);

        this.player.Completion += this.OnPlaybackCompleted;

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
            if (this.player.IsPlaying)
                this.player.Stop();
            this.player.Reset();
            this.player.Release();
            this.player = null;
        }
        this.currentTrack = null;
        this.SetState(PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        this.player?.SeekTo((int)position.TotalMilliseconds);
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

    void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        this.SetState(PlaybackState.Stopped);
        this.PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }
}
