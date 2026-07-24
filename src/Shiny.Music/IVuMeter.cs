namespace Shiny.Music;

/// <summary>
/// A VU meter that raises periodic <see cref="LevelChanged"/> events for the current playback. Create one
/// with <see cref="IMusicPlayer.CreateVuMeter"/>, call <see cref="Start"/> to begin, and <see cref="Stop"/>
/// or dispose to end.
/// <para>
/// <b>Threading:</b> <see cref="LevelChanged"/> may be raised on a background thread — marshal to the UI
/// thread before touching UI state.
/// </para>
/// </summary>
public interface IVuMeter : IDisposable
{
    /// <summary>Raised each sampling interval (or capture callback) with the latest <see cref="VuLevel"/>.</summary>
    event EventHandler<VuLevel>? LevelChanged;

    /// <summary>The most recent level (<see cref="VuLevel.Silent"/> before the first reading).</summary>
    VuLevel Current { get; }

    /// <summary>
    /// <c>true</c> when the levels come from a real audio-output tap (Android <c>Visualizer</c>);
    /// <c>false</c> when they are implied from the offline analysis synced to the playback position
    /// (Apple platforms, where the system music player exposes no output tap).
    /// </summary>
    bool IsLive { get; }

    /// <summary>Begins emitting <see cref="LevelChanged"/> events. Idempotent.</summary>
    void Start();

    /// <summary>Stops emitting events. The meter can be started again.</summary>
    void Stop();
}
