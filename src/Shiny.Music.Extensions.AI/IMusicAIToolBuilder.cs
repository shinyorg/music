namespace Shiny.Music.Extensions.AI;

/// <summary>
/// Opt-in builder for the music capabilities an AI agent is allowed to access. Anything not added
/// here is invisible to the LLM. Areas are additive — call several to expose more tools.
/// </summary>
public interface IMusicAIToolBuilder
{
    /// <summary>
    /// Exposes read-only library tools: searching and browsing the track library (by genre, era,
    /// or free text — the natural path for "pick a song for my mood"), listing genres / years /
    /// decades, browsing playlists and their tracks, and fetching lyrics. Requires
    /// <see cref="IMediaLibrary"/> (and <see cref="ILyricsProvider"/> for lyrics) in DI.
    /// </summary>
    IMusicAIToolBuilder AddLibrary();

    /// <summary>
    /// Exposes playback control tools: play a track by id, pause / resume / stop / seek, and read
    /// the current "now playing" status. Requires <see cref="IMusicPlayer"/> in DI.
    /// </summary>
    IMusicAIToolBuilder AddPlayback();

    /// <summary>
    /// Exposes tools that create, modify, and delete the user's custom (locally-stored) playlists.
    /// Requires <see cref="IMediaLibrary"/> in DI.
    /// </summary>
    IMusicAIToolBuilder AddPlaylistManagement();

    /// <summary>Exposes every area in one call (library, playback, and playlist management).</summary>
    IMusicAIToolBuilder AddAll();
}
