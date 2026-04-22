namespace Shiny.Music;

/// <summary>
/// Contains lyrics for a music track.
/// </summary>
/// <param name="PlainLyrics">The plain text (unsynchronized) lyrics, or <c>null</c> if unavailable.</param>
/// <param name="SyncedLyrics">The synchronized lyrics in LRC format with timestamps, or <c>null</c> if unavailable.</param>
public record LyricsResult(string? PlainLyrics, string? SyncedLyrics);
