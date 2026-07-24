namespace Shiny.Music;

/// <summary>
/// The result of an <b>offline</b> level analysis of a track — computed by decoding the audio to PCM
/// and measuring amplitude, <b>without</b> playing anything through the speakers. Suitable for drawing
/// a waveform / VU meter, or for locating loud/quiet regions of a song (see <see cref="Sections"/>).
/// <para>
/// Produced by <see cref="IMediaLibrary.AnalyzeLevelsAsync"/>, which returns <c>null</c> for
/// DRM-protected tracks that cannot be decoded to PCM (the same tracks
/// <see cref="IMediaLibrary.CopyTrackAsync"/> refuses).
/// </para>
/// </summary>
/// <param name="Window">The duration each entry in <see cref="Rms"/> / <see cref="Peak"/> represents.</param>
/// <param name="Duration">The total duration of the analyzed track.</param>
/// <param name="Rms">
/// Per-window RMS (average) level, normalized 0.0–1.0 against the loudest sample in the track. This is
/// the "VU" envelope. Entry <c>i</c> covers <c>[i * Window, (i + 1) * Window)</c>.
/// </param>
/// <param name="Peak">
/// Per-window peak (maximum absolute) level, normalized 0.0–1.0 against the loudest sample in the track,
/// aligned one-to-one with <see cref="Rms"/>.
/// </param>
/// <param name="Sections">
/// The track collapsed into contiguous same-energy <see cref="AudioSection"/> runs — a compact "song
/// structure" for locating a solo, chorus, or intro.
/// </param>
public record AudioLevels(
    TimeSpan Window,
    TimeSpan Duration,
    IReadOnlyList<float> Rms,
    IReadOnlyList<float> Peak,
    IReadOnlyList<AudioSection> Sections
)
{
    /// <summary>
    /// Returns the <see cref="VuLevel"/> for a playback position by indexing the envelope at that point.
    /// This is the "implied" VU used where a live output tap isn't available (Apple platforms). Positions
    /// outside the track clamp to the nearest window.
    /// </summary>
    /// <param name="position">The playback position to sample.</param>
    public VuLevel SampleAt(TimeSpan position)
    {
        if (this.Rms.Count == 0 || this.Window <= TimeSpan.Zero)
            return VuLevel.Silent with { Position = position };

        var index = Math.Clamp((int)(position.TotalSeconds / this.Window.TotalSeconds), 0, this.Rms.Count - 1);

        var energy = AudioEnergy.Silent;
        foreach (var section in this.Sections)
        {
            if (position >= section.Start && position < section.End)
            {
                energy = section.Energy;
                break;
            }
        }

        return new VuLevel(position, this.Rms[index], this.Peak[index], energy);
    }
}
