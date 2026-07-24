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
);
