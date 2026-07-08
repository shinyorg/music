using Android.Media;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

public class MusicPlayer(PlayCountStore playCounts) : IMusicPlayer
{
    Android.Media.MediaPlayer? player;
    MusicMetadata? currentTrack;
    PlaybackState state = PlaybackState.Stopped;

    DuckScope? activeDuck;
    CancellationTokenSource? fadeCts;
    float currentVolume = 1f;

    public PlaybackState State => this.state;
    public MusicMetadata? CurrentTrack => this.currentTrack;
    public bool IsDucked => this.activeDuck != null;

    public TimeSpan Position =>
        this.player != null ? TimeSpan.FromMilliseconds(this.player.CurrentPosition) : TimeSpan.Zero;

    public TimeSpan Duration =>
        this.player != null ? TimeSpan.FromMilliseconds(this.player.Duration) : TimeSpan.Zero;

    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler? PlaybackCompleted;

    public Task PlayAsync(MusicMetadata track)
    {
        this.Stop();

        this.player = new Android.Media.MediaPlayer();
        this.player.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        var uri = Uri.Parse(track.ContentUri)!;
        this.player.SetDataSource(Application.Context, uri);
        this.player.Prepare();
        this.player.Start();

        this.currentTrack = track;
        this.SetState(PlaybackState.Playing);

        this.player.Completion += this.OnPlaybackCompleted;

        _ = playCounts.IncrementAsync(track.Id);
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
        this.fadeCts?.Cancel();
        this.fadeCts = null;
        this.activeDuck = null;
        this.currentVolume = 1f;

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

    public IAsyncDisposable Duck(DuckOptions? options = null)
    {
        if (this.player == null || this.state != PlaybackState.Playing)
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

        var start = this.currentVolume;
        var steps = duration <= TimeSpan.Zero ? 1 : Math.Max(1, (int)(duration.TotalMilliseconds / 15));

        try
        {
            for (var i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                var v = start + ((target - start) * (i / (float)steps));
                this.SetVolume(v);
                if (i < steps)
                    await Task.Delay(15, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer fade; leave volume where it landed.
        }
    }

    void SetVolume(float volume)
    {
        this.currentVolume = volume;
        this.player?.SetVolume(volume, volume);
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
