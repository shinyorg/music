using Android.Media;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

public class MusicPlayer : IMusicPlayer
{
    Android.Media.MediaPlayer? _player;
    MusicMetadata? _currentTrack;
    PlaybackState _state = PlaybackState.Stopped;

    public PlaybackState State => _state;
    public MusicMetadata? CurrentTrack => _currentTrack;

    public TimeSpan Position =>
        _player != null ? TimeSpan.FromMilliseconds(_player.CurrentPosition) : TimeSpan.Zero;

    public TimeSpan Duration =>
        _player != null ? TimeSpan.FromMilliseconds(_player.Duration) : TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public Task PlayAsync(MusicMetadata track)
    {
        Stop();

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current activity available");

        _player = new Android.Media.MediaPlayer();
        _player.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        var uri = Uri.Parse(track.ContentUri)!;
        _player.SetDataSource(activity, uri);
        _player.Prepare();
        _player.Start();

        _currentTrack = track;
        SetState(PlaybackState.Playing);

        _player.Completion += OnPlaybackCompleted;

        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (_player != null && _state == PlaybackState.Playing)
        {
            _player.Pause();
            SetState(PlaybackState.Paused);
        }
    }

    public void Resume()
    {
        if (_player != null && _state == PlaybackState.Paused)
        {
            _player.Start();
            SetState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        if (_player != null)
        {
            _player.Completion -= OnPlaybackCompleted;
            if (_player.IsPlaying)
                _player.Stop();
            _player.Reset();
            _player.Release();
            _player = null;
        }
        _currentTrack = null;
        SetState(PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        _player?.SeekTo((int)position.TotalMilliseconds);
    }

    public void Dispose()
    {
        Stop();
    }

    void SetState(PlaybackState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        SetState(PlaybackState.Stopped);
        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }
}
