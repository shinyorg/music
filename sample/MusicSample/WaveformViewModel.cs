using CommunityToolkit.Mvvm.ComponentModel;
using Shiny.Music;

namespace MusicSample;

/// <summary>
/// Drives the waveform + VU page. It runs an offline <see cref="IMediaLibrary.AnalyzeLevelsAsync"/> on the
/// currently-playing track, then a fast dispatcher timer advances the play-head and reads the current
/// window's RMS / peak from the precomputed envelope to animate the VU meter — all without any extra
/// playback or audio-tap.
/// </summary>
public partial class WaveformViewModel : ObservableObject
{
    // A fine analysis window makes the VU meter lively; the waveform itself is down-sampled to the view width.
    static readonly TimeSpan AnalysisWindow = TimeSpan.FromMilliseconds(80);

    readonly IMusicPlayer player;
    readonly IMediaLibrary library;
    IDispatcherTimer? timer;
    IDispatcher? dispatcher;
    bool isScrubbing;

    public WaveformViewModel(IMusicPlayer player, IMediaLibrary library)
    {
        this.player = player;
        this.library = library;

        var track = player.CurrentTrack;
        this.Title = track?.Title ?? "Not Playing";
        this.Artist = track?.Artist ?? "";
    }

    [ObservableProperty] string title;
    [ObservableProperty] string artist;
    [ObservableProperty] string statusText = "Analyzing…";
    [ObservableProperty] bool isBusy = true;
    [ObservableProperty] bool waveformAvailable;
    [ObservableProperty] string positionText = "0:00";
    [ObservableProperty] string durationText = "0:00";

    /// <summary>Normalized per-window RMS envelope (0..1), or <c>null</c> when analysis is unavailable (DRM).</summary>
    public IReadOnlyList<float>? Rms { get; private set; }
    public IReadOnlyList<float>? Peak { get; private set; }

    /// <summary>0..1 play-head position across the track (also used while scrubbing).</summary>
    public double Progress { get; private set; }

    /// <summary>Current VU levels (0..1) at the play-head, with a little peak-hold decay for a natural meter feel.</summary>
    public float VuRms { get; private set; }
    public float VuPeak { get; private set; }

    float peakHold;

    /// <summary>Raised whenever the visuals should be redrawn (play-head moved, levels changed, analysis loaded).</summary>
    public event EventHandler? Invalidated;

    public void SetDispatcher(IDispatcher disp) => this.dispatcher = disp;

    public async Task InitializeAsync()
    {
        var track = this.player.CurrentTrack;
        if (track is null)
        {
            this.IsBusy = false;
            this.StatusText = "No song is playing.";
            RaiseInvalidated();
            return;
        }

        this.DurationText = FormatTime(this.player.Duration);

        // Make the page interactive immediately: the plain progress bar + seek work right away and the
        // play-head starts moving, while the (whole-song) analysis runs in the background and fills in the
        // waveform + VU when it's ready. This is what keeps it from looking like it "loads forever".
        this.IsBusy = false;
        this.StatusText = "Analyzing waveform…";
        StartTimer();
        Tick();

        AudioLevels? levels = null;
        var timedOut = false;
        try
        {
            var analyze = this.library.AnalyzeLevelsAsync(track.Id, AnalysisWindow);
            var finished = await Task.WhenAny(analyze, Task.Delay(TimeSpan.FromSeconds(45)));
            if (finished == analyze)
                levels = await analyze;
            else
                timedOut = true;
        }
        catch
        {
            // fall through to the unavailable state
        }

        if (levels is null)
        {
            // DRM-protected / streaming-only (or too slow) — no envelope. The progress bar + seek still work.
            this.WaveformAvailable = false;
            this.StatusText = timedOut
                ? "Waveform analysis timed out — progress & seek still work."
                : "Waveform unavailable (DRM-protected track) — progress & seek still work.";
        }
        else
        {
            this.Rms = levels.Rms;
            this.Peak = levels.Peak;
            this.WaveformAvailable = true;
            this.StatusText = $"{levels.Sections.Count} sections • {levels.Rms.Count} windows @ {levels.Window.TotalMilliseconds:0}ms";
        }

        RaiseInvalidated();
    }

    public void Stop() => StopTimer();

    // ── Scrub / seek from the waveform ──────────────────────────────

    public void BeginScrub() => this.isScrubbing = true;

    public void ScrubTo(double fraction)
    {
        if (!this.isScrubbing) return;
        Progress = Math.Clamp(fraction, 0, 1);
        PositionText = FormatTime(TimeSpan.FromSeconds(Progress * this.player.Duration.TotalSeconds));
        UpdateVu();
        RaiseInvalidated();
    }

    public void EndScrub(double fraction)
    {
        this.isScrubbing = false;
        SeekTo(fraction);
    }

    public void SeekTo(double fraction)
    {
        var duration = this.player.Duration;
        if (duration.TotalSeconds <= 0) return;

        var clamped = Math.Clamp(fraction, 0, 1);
        this.player.Seek(TimeSpan.FromSeconds(clamped * duration.TotalSeconds));
        Progress = clamped;
        PositionText = FormatTime(TimeSpan.FromSeconds(clamped * duration.TotalSeconds));
        UpdateVu();
        RaiseInvalidated();
    }

    // ── Timer ───────────────────────────────────────────────────────

    void StartTimer()
    {
        if (this.timer != null || this.dispatcher == null) return;
        this.timer = this.dispatcher.CreateTimer();
        this.timer.Interval = TimeSpan.FromMilliseconds(60);
        this.timer.Tick += (_, _) => Tick();
        this.timer.Start();
    }

    void StopTimer()
    {
        if (this.timer == null) return;
        this.timer.Stop();
        this.timer = null;
    }

    void Tick()
    {
        if (this.isScrubbing) return;

        var position = this.player.Position;
        var duration = this.player.Duration;

        Progress = duration.TotalSeconds > 0 ? position.TotalSeconds / duration.TotalSeconds : 0;
        PositionText = FormatTime(position);
        DurationText = FormatTime(duration);
        UpdateVu();
        RaiseInvalidated();
    }

    void UpdateVu()
    {
        if (this.Rms is null || this.Peak is null || this.Rms.Count == 0)
        {
            VuRms = 0;
            VuPeak = 0;
            return;
        }

        var index = (int)(Progress * (this.Rms.Count - 1));
        index = Math.Clamp(index, 0, this.Rms.Count - 1);

        var isPlaying = this.player.State == PlaybackState.Playing || this.isScrubbing;
        VuRms = isPlaying ? this.Rms[index] : 0;

        var instantPeak = isPlaying ? this.Peak[index] : 0;
        // Peak-hold with decay so the meter falls back smoothly rather than flickering.
        this.peakHold = instantPeak >= this.peakHold ? instantPeak : Math.Max(instantPeak, this.peakHold - 0.06f);
        VuPeak = this.peakHold;
    }

    void RaiseInvalidated() => this.Invalidated?.Invoke(this, EventArgs.Empty);

    static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
}
