namespace Shiny.Music.Internal;

/// <summary>
/// The "implied" VU meter: on an interval it reads the player's position and samples the precomputed
/// <see cref="AudioLevels"/> envelope at that point. Used on Apple platforms (no output tap) and as the
/// Android fallback when the live <c>Visualizer</c> isn't available. <see cref="IVuMeter.IsLive"/> is false.
/// </summary>
sealed class SampledVuMeter : IVuMeter
{
    readonly IMusicPlayer player;
    readonly AudioLevels? levels;
    readonly TimeSpan interval;
    Timer? timer;

    public SampledVuMeter(IMusicPlayer player, AudioLevels? levels, TimeSpan interval)
    {
        this.player = player;
        this.levels = levels;
        this.interval = interval;
        this.Current = VuLevel.Silent;
    }

    public event EventHandler<VuLevel>? LevelChanged;
    public VuLevel Current { get; private set; }
    public bool IsLive => false;

    public void Start()
    {
        if (this.timer != null)
            return;

        this.timer = new Timer(_ => Tick(), null, TimeSpan.Zero, this.interval);
    }

    public void Stop()
    {
        this.timer?.Dispose();
        this.timer = null;
    }

    void Tick()
    {
        var position = this.player.Position;
        var level = this.player.State == PlaybackState.Playing && this.levels != null
            ? this.levels.SampleAt(position)
            : VuLevel.Silent with { Position = position };

        this.Current = level;
        this.LevelChanged?.Invoke(this, level);
    }

    public void Dispose() => this.Stop();
}
