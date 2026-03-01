using AVFoundation;
using Foundation;

namespace Shiny.Music;

public class MusicPlayer : IMusicPlayer
{
    AVAudioPlayer? _player;
    MusicMetadata? _currentTrack;
    PlaybackState _state = PlaybackState.Stopped;

    public PlaybackState State => _state;
    public MusicMetadata? CurrentTrack => _currentTrack;

    public TimeSpan Position =>
        _player != null ? TimeSpan.FromSeconds(_player.CurrentTime) : TimeSpan.Zero;

    public TimeSpan Duration =>
        _player != null ? TimeSpan.FromSeconds(_player.Duration) : TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public Task PlayAsync(MusicMetadata track)
    {
        Stop();

        var url = NSUrl.FromString(track.ContentUri);
        if (url == null)
            throw new InvalidOperationException($"Cannot play track: invalid content URI '{track.ContentUri}'");

        // Activate audio session for playback
        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Playback);
        audioSession.SetActive(true);

        _player = AVAudioPlayer.FromUrl(url, out var error);
        if (_player == null || error != null)
            throw new InvalidOperationException($"Failed to create player: {error?.LocalizedDescription ?? "unknown error"}");

        _player.FinishedPlaying += OnFinishedPlaying;
        _player.PrepareToPlay();
        _player.Play();

        _currentTrack = track;
        SetState(PlaybackState.Playing);

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
            _player.Play();
            SetState(PlaybackState.Playing);
        }
    }

    public void Stop()
    {
        if (_player != null)
        {
            _player.FinishedPlaying -= OnFinishedPlaying;
            _player.Stop();
            _player.Dispose();
            _player = null;
        }
        _currentTrack = null;
        SetState(PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        if (_player != null)
            _player.CurrentTime = position.TotalSeconds;
    }

    public void Dispose()
    {
        Stop();
        var audioSession = AVAudioSession.SharedInstance();
        audioSession.SetActive(false);
    }

    void SetState(PlaybackState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    void OnFinishedPlaying(object? sender, AVStatusEventArgs e)
    {
        if (e.Status)
        {
            SetState(PlaybackState.Stopped);
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
