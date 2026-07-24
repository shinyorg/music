using System.Globalization;
using System.Text.RegularExpressions;

namespace Shiny.Music;

/// <summary>
/// Helpers for working with <see cref="LyricsResult"/> — parsing LRC (time-synced) lyrics and deriving
/// the instrumental (no-vocal) sections of a track from the lyric timeline alone. These are the
/// DRM-safe way to locate "the part between the verses" without decoding any audio.
/// </summary>
public static partial class LyricsExtensions
{
    /// <summary>The default minimum span, with no sung line, that counts as an instrumental gap.</summary>
    public static readonly TimeSpan DefaultMinimumGap = TimeSpan.FromSeconds(8);

    // Matches an LRC time tag: [mm:ss], [mm:ss.xx], or [mm:ss.xxx]. Rejects metadata tags like [ar:...].
    [GeneratedRegex(@"\[(\d{1,3}):([0-5]?\d)(?:[.:](\d{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex TimeTagRegex();

    /// <summary>
    /// Parses the <see cref="LyricsResult.SyncedLyrics"/> LRC document into timestamped lines, sorted by
    /// time. A line carrying several time tags (a repeated chorus) yields one <see cref="LrcLine"/> per tag.
    /// Returns an empty list when there are no synced lyrics to parse.
    /// </summary>
    /// <param name="lyrics">The lyrics to parse. May be <c>null</c>.</param>
    /// <returns>The parsed lines ordered by <see cref="LrcLine.Timestamp"/>.</returns>
    public static IReadOnlyList<LrcLine> ParseSyncedLyrics(this LyricsResult? lyrics)
        => ParseSyncedLyrics(lyrics?.SyncedLyrics);

    /// <summary>
    /// Parses a raw LRC document into timestamped lines, sorted by time.
    /// </summary>
    /// <param name="lrc">The raw LRC text. May be <c>null</c> or empty.</param>
    /// <returns>The parsed lines ordered by <see cref="LrcLine.Timestamp"/>.</returns>
    public static IReadOnlyList<LrcLine> ParseSyncedLyrics(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
            return Array.Empty<LrcLine>();

        var lines = new List<LrcLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var matches = TimeTagRegex().Matches(raw);
            if (matches.Count == 0)
                continue;

            // The lyric text is whatever remains after stripping the leading time tag(s).
            var text = TimeTagRegex().Replace(raw, string.Empty).Trim();

            foreach (Match m in matches)
            {
                var minutes = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var seconds = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);

                var fraction = 0d;
                if (m.Groups[3].Success)
                {
                    var frac = m.Groups[3].Value;
                    fraction = int.Parse(frac, CultureInfo.InvariantCulture) / Math.Pow(10, frac.Length);
                }

                var ts = TimeSpan.FromMilliseconds((minutes * 60 + seconds) * 1000d + fraction * 1000d);
                lines.Add(new LrcLine(ts, text));
            }
        }

        lines.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return lines;
    }

    /// <summary>
    /// Derives the instrumental (no-vocal) gaps of a track from its time-synced lyrics — the intro before
    /// the first line, any stretch between sung lines longer than <paramref name="minimumGap"/> (a bridge,
    /// break, or solo), and the outro after the last line. Requires <see cref="LyricsResult.SyncedLyrics"/>;
    /// returns an empty list when only plain lyrics (or none) are available.
    /// <para>
    /// This is the DRM-safe complement to <see cref="IMediaLibrary.AnalyzeLevelsAsync"/>: it needs no audio
    /// decode, so it works for protected tracks. Combine the two when both are available — the lyric gaps
    /// give precise boundaries, the audio energy tells you which gap is the loud solo versus the quiet intro.
    /// </para>
    /// </summary>
    /// <param name="lyrics">The lyrics to analyze. May be <c>null</c>.</param>
    /// <param name="trackDuration">
    /// The total track duration, used to measure the trailing (outro) gap after the final sung line. When
    /// <c>null</c>, no trailing gap is produced.
    /// </param>
    /// <param name="minimumGap">
    /// The shortest no-vocal span that counts as instrumental. Defaults to <see cref="DefaultMinimumGap"/>
    /// (8 seconds), which excludes ordinary pauses between lines.
    /// </param>
    /// <returns>The instrumental gaps in track order.</returns>
    public static IReadOnlyList<InstrumentalGap> GetInstrumentalGaps(
        this LyricsResult? lyrics,
        TimeSpan? trackDuration = null,
        TimeSpan? minimumGap = null)
    {
        var minGap = minimumGap ?? DefaultMinimumGap;
        var lines = lyrics.ParseSyncedLyrics();
        if (lines.Count == 0)
            return Array.Empty<InstrumentalGap>();

        var vocals = lines.Where(l => l.HasText).ToList();
        if (vocals.Count == 0)
            return Array.Empty<InstrumentalGap>();

        var gaps = new List<InstrumentalGap>();

        // Leading intro gap: from the start of the track to the first sung line.
        if (vocals[0].Timestamp >= minGap)
            gaps.Add(new InstrumentalGap(TimeSpan.Zero, vocals[0].Timestamp));

        // Between consecutive sung lines. If the LRC placed a blank marker line where singing stops,
        // prefer that as the gap start (more precise); otherwise start at the previous sung line.
        for (var i = 0; i < vocals.Count - 1; i++)
        {
            var from = vocals[i].Timestamp;
            var to = vocals[i + 1].Timestamp;

            var marker = lines
                .Where(l => !l.HasText && l.Timestamp > from && l.Timestamp < to)
                .Select(l => (TimeSpan?)l.Timestamp)
                .LastOrDefault();

            var start = marker ?? from;
            if (to - start >= minGap)
                gaps.Add(new InstrumentalGap(start, to - start));
        }

        // Trailing outro gap: from the last sung line (or a later blank marker) to the end of the track.
        if (trackDuration is TimeSpan duration)
        {
            var last = vocals[^1].Timestamp;
            var marker = lines
                .Where(l => !l.HasText && l.Timestamp > last && l.Timestamp <= duration)
                .Select(l => (TimeSpan?)l.Timestamp)
                .LastOrDefault();

            var start = marker ?? last;
            if (duration - start >= minGap)
                gaps.Add(new InstrumentalGap(start, duration - start));
        }

        return gaps;
    }
}
