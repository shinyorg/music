using CommunityToolkit.Mvvm.ComponentModel;
using Shiny.Music;

namespace MusicSample;

/// <summary>
/// Drives the waveform + VU page. It runs an offline <see cref="IMediaLibrary.AnalyzeLevelsAsync"/> on the
/// currently-playing track for the waveform, and drives the VU meter from an <see cref="IVuMeter"/> — a real
/// audio-output tap on Android, or the "implied" (analysis-synced-to-position) meter on Apple. A dispatcher
/// timer advances the play-head / progress.
/// </summary>
public partial class WaveformViewModel : ObservableObject
{
    // A fine analysis window makes the implied VU lively; the waveform itself is down-sampled to the view width.
    static readonly TimeSpan AnalysisWindow = TimeSpan.FromMilliseconds(80);

    readonly IMusicPlayer player;
    readonly IMediaLibrary library;
    IDispatcherTimer? timer;
    IDispatcher? dispatcher;
    IVuMeter? vuMeter;
    bool isScrubbing;
    float peakHold;

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

    /// <summary>Current VU levels (0..1), from the <see cref="IVuMeter"/> with a little peak-hold decay.</summary>
    public float VuRms { get; private set; }
    public float VuPeak { get; private set; }

    /// <summary>Raised whenever the visuals should be redrawn (play-head moved, VU changed, analysis loaded).</summary>
    public event EventHandler? Invalidated;

    /// <summary>Raised (with a user-facing message) when the track cannot be analyzed — the page shows an alert and navigates back.</summary>
    public event EventHandler<string>? Unavailable;

    public void SetDispatcher(IDispatcher disp) => this.dispatcher = disp;

    public async Task InitializeAsync()
    {
        var track = this.player.CurrentTrack;
        if (track is null)
        {
            RaiseUnavailable("No song is playing.");
            return;
        }

        this.DurationText = FormatTime(this.player.Duration);
        this.StatusText = "Analyzing waveform…";
        this.IsBusy = true;

        AudioLevels? levels = null;
        try
        {
            var analyze = this.library.AnalyzeLevelsAsync(track.Id, AnalysisWindow);
            // Guard against a decode that stalls so we never spin forever.
            var finished = await Task.WhenAny(analyze, Task.Delay(TimeSpan.FromSeconds(45)));
            if (finished == analyze)
                levels = await analyze;
        }
        catch
        {
            // treated as unavailable below
        }

        this.IsBusy = false;

        if (levels is null)
        {
            // DRM-protected, streaming-only, or the audio couldn't be read — tell the user and go back.
            RaiseUnavailable("This track can't be analyzed. It may be DRM-protected, or its audio couldn't be read on this device.");
            return;
        }

        this.Rms = levels.Rms;
        this.Peak = levels.Peak;
        this.WaveformAvailable = true;

#if ANDROID
        // The live Android VU meter taps the audio output, which the OS gates behind RECORD_AUDIO.
        try { await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Microphone>(); }
        catch { /* fall back to the implied meter if denied */ }
#endif

        // Live output tap on Android (when permitted); implied (analysis @ position) on Apple.
        this.vuMeter = this.player.CreateVuMeter(levels, TimeSpan.FromMilliseconds(50));
        this.vuMeter.LevelChanged += OnVuLevel;
        this.vuMeter.Start();

        var meterKind = this.vuMeter.IsLive ? "live VU" : "implied VU";
        this.StatusText = $"{levels.Sections.Count} sections • {meterKind}";

        StartTimer();
        Tick();
        RaiseInvalidated();
    }

    void OnVuLevel(object? sender, VuLevel level)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            this.VuRms = level.Rms;
            // Peak-hold with decay so the meter falls back smoothly rather than flickering.
            this.peakHold = level.Peak >= this.peakHold ? level.Peak : Math.Max(level.Peak, this.peakHold - 0.06f);
            this.VuPeak = this.peakHold;
            RaiseInvalidated();
        });
    }

    void RaiseUnavailable(string message)
    {
        this.IsBusy = false;
        StopTimer();
        this.Unavailable?.Invoke(this, message);
    }

    public void Stop()
    {
        StopTimer();
        if (this.vuMeter != null)
        {
            this.vuMeter.LevelChanged -= OnVuLevel;
            this.vuMeter.Dispose();
            this.vuMeter = null;
        }
    }

    // ── Scrub / seek from the waveform ──────────────────────────────

    public void BeginScrub() => this.isScrubbing = true;

    public void ScrubTo(double fraction)
    {
        if (!this.isScrubbing) return;
        Progress = Math.Clamp(fraction, 0, 1);
        PositionText = FormatTime(TimeSpan.FromSeconds(Progress * this.player.Duration.TotalSeconds));
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
        RaiseInvalidated();
    }

    // ── Timer (play-head / progress) ────────────────────────────────

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
        RaiseInvalidated();
    }

    void RaiseInvalidated() => this.Invalidated?.Invoke(this, EventArgs.Empty);

    static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
}
