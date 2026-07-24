namespace Shiny.Music;

/// <summary>
/// A single time-synced lyric line parsed from an LRC document
/// (see <see cref="LyricsExtensions.ParseSyncedLyrics"/>).
/// </summary>
/// <param name="Timestamp">The offset from the start of the track at which this line is sung.</param>
/// <param name="Text">
/// The lyric text, trimmed. May be empty for blank marker lines that some LRC files use to indicate
/// where singing pauses (the start of an instrumental passage).
/// </param>
public record LrcLine(TimeSpan Timestamp, string Text)
{
    /// <summary>Whether this line carries sung text (as opposed to a blank spacing/marker line).</summary>
    public bool HasText => this.Text.Length > 0;
}
