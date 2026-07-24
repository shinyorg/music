namespace Shiny.Music;

/// <summary>
/// A contiguous stretch of a track whose windows share the same relative <see cref="AudioEnergy"/>.
/// Sections are derived from the offline level analysis in <see cref="AudioLevels"/> and are the
/// coarse "song structure" a caller (or an LLM) reasons over — e.g. finding the loud instrumental
/// stretch that is likely a solo, or the quiet intro to skip past.
/// </summary>
/// <param name="Start">Offset of the section's start from the beginning of the track.</param>
/// <param name="Duration">Length of the section.</param>
/// <param name="Energy">The section's relative energy classification.</param>
/// <param name="AverageLevel">
/// The section's mean RMS level, normalized 0.0–1.0 against the loudest sample in the track
/// (1.0 = as loud as the track ever gets).
/// </param>
public record AudioSection(TimeSpan Start, TimeSpan Duration, AudioEnergy Energy, float AverageLevel)
{
    /// <summary>The offset of the section's end from the beginning of the track (<see cref="Start"/> + <see cref="Duration"/>).</summary>
    public TimeSpan End => this.Start + this.Duration;
}
