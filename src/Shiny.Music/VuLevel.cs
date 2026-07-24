namespace Shiny.Music;

/// <summary>
/// A single VU reading emitted by an <see cref="IVuMeter"/>: the RMS (average) and peak levels at a point
/// in playback, normalized 0.0–1.0. Depending on <see cref="IVuMeter.IsLive"/> these are either measured
/// from the real audio output (Android) or implied from the offline analysis at the current position (Apple).
/// </summary>
/// <param name="Position">The playback position this reading corresponds to.</param>
/// <param name="Rms">The RMS (average) level, normalized 0.0–1.0.</param>
/// <param name="Peak">The peak (maximum) level, normalized 0.0–1.0.</param>
/// <param name="Energy">A coarse energy classification of this moment.</param>
public record VuLevel(TimeSpan Position, float Rms, float Peak, AudioEnergy Energy)
{
    /// <summary>A zeroed reading — used when nothing is playing.</summary>
    public static readonly VuLevel Silent = new(TimeSpan.Zero, 0f, 0f, AudioEnergy.Silent);
}
