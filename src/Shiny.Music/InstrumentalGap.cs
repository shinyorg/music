namespace Shiny.Music;

/// <summary>
/// A stretch of a track with no sung lyrics — an intro, an instrumental break, a solo, or an outro —
/// derived purely from time-synced (LRC) lyrics via <see cref="LyricsExtensions.GetInstrumentalGaps"/>.
/// <para>
/// Because this is computed from lyric timestamps alone (no audio decode), it works even for
/// DRM-protected tracks where <see cref="IMediaLibrary.AnalyzeLevelsAsync"/> returns <c>null</c>.
/// The <see cref="Start"/> is where the previous sung line begins (or an explicit blank-line marker,
/// when the LRC provides one), so the true instrumental may begin a beat later.
/// </para>
/// </summary>
/// <param name="Start">Offset of the gap's start from the beginning of the track.</param>
/// <param name="Duration">Length of the gap.</param>
public record InstrumentalGap(TimeSpan Start, TimeSpan Duration)
{
    /// <summary>The offset of the gap's end from the beginning of the track (<see cref="Start"/> + <see cref="Duration"/>).</summary>
    public TimeSpan End => this.Start + this.Duration;
}
