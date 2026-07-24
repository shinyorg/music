using Android.Content.PM;
using Android.Media.Audiofx;

namespace Shiny.Music;

/// <summary>
/// A <b>real</b> VU meter that taps the audio output via <c>android.media.audiofx.Visualizer</c> attached to
/// the player's audio session. Requires the <c>RECORD_AUDIO</c> permission (the OS treats output capture like
/// recording). Emits a <see cref="VuLevel"/> from each waveform capture callback.
/// </summary>
sealed class VisualizerVuMeter : Java.Lang.Object, IVuMeter, Visualizer.IOnDataCaptureListener
{
    readonly IMusicPlayer player;
    readonly int sessionId;
    readonly TimeSpan interval;
    Visualizer? visualizer;

    public VisualizerVuMeter(IMusicPlayer player, int sessionId, TimeSpan interval)
    {
        this.player = player;
        this.sessionId = sessionId;
        this.interval = interval;
        this.Current = VuLevel.Silent;
    }

    public event EventHandler<VuLevel>? LevelChanged;
    public VuLevel Current { get; private set; }
    public bool IsLive => true;

    /// <summary>Whether the app currently holds RECORD_AUDIO (required to construct a <c>Visualizer</c>).</summary>
    public static bool HasPermission()
        => Application.Context.CheckSelfPermission(Android.Manifest.Permission.RecordAudio) == Permission.Granted;

    public void Start()
    {
        if (this.visualizer != null)
            return;

        var v = new Visualizer(this.sessionId);
        v.SetCaptureSize(Visualizer.GetCaptureSizeRange()[1]);   // max, for a smoother RMS (must precede SetEnabled)

        // Capture rate is in milliHz; map the requested interval to a rate and clamp to the device maximum.
        var rateMilliHz = Math.Min((int)(1000.0 / Math.Max(this.interval.TotalSeconds, 0.001)), Visualizer.MaxCaptureRate);
        v.SetDataCaptureListener(this, rateMilliHz, waveform: true, fft: false);
        v.SetEnabled(true);
        this.visualizer = v;
    }

    public void Stop()
    {
        if (this.visualizer == null)
            return;

        try { this.visualizer.SetEnabled(false); }
        catch { /* already released */ }

        this.visualizer.Release();
        this.visualizer.Dispose();
        this.visualizer = null;
    }

    public void OnWaveFormDataCapture(Visualizer? v, byte[]? waveform, int samplingRate)
    {
        if (waveform is null || waveform.Length == 0)
            return;

        double sumSquares = 0;
        float peak = 0;
        foreach (var b in waveform)
        {
            // Waveform is 8-bit unsigned PCM, centered at 128.
            var sample = (b - 128) / 128f;
            sumSquares += (double)sample * sample;
            var magnitude = Math.Abs(sample);
            if (magnitude > peak)
                peak = magnitude;
        }

        var rms = (float)Math.Sqrt(sumSquares / waveform.Length);
        var level = new VuLevel(this.player.Position, Math.Clamp(rms, 0f, 1f), Math.Clamp(peak, 0f, 1f), Classify(rms));
        this.Current = level;
        this.LevelChanged?.Invoke(this, level);
    }

    public void OnFftDataCapture(Visualizer? v, byte[]? fft, int samplingRate)
    {
        // Not used — we only capture the waveform.
    }

    // Live capture has no per-track maximum to normalize against, so classify by fixed thresholds.
    static AudioEnergy Classify(float rms) => rms switch
    {
        < 0.02f => AudioEnergy.Silent,
        < 0.15f => AudioEnergy.Quiet,
        < 0.40f => AudioEnergy.Moderate,
        _ => AudioEnergy.Loud
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.Stop();
        base.Dispose(disposing);
    }
}
