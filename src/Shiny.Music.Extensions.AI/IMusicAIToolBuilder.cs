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
    /// decades, browsing playlists and their tracks, fetching lyrics, and analyzing a track's
    /// structure (instrumental gaps and audio-energy sections) so an agent can start playback at a
    /// specific moment such as a solo. Requires <see cref="IMediaLibrary"/> (and
    /// <see cref="ILyricsProvider"/> for lyrics / lyric-based gap detection) in DI.
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

    /// <summary>
    /// Exposes the <c>search_catalog</c> tool — searching the Apple Music streaming catalog for songs
    /// that need not be in the user's library, playable through <c>play_track</c> when the
    /// <see cref="AddPlayback"/> area is also enabled. Requires <see cref="IMediaLibrary"/> in DI.
    /// <para>
    /// <b>Apple platforms only.</b> The underlying <see cref="IMediaLibrary.SearchCatalogAsync"/> throws
    /// on Android (the tool reports it as an error to the model). It is deliberately <b>not</b> included in
    /// <see cref="AddAll"/> — guard the call with <c>#if IOS</c> (or an Apple runtime check) so the tool is
    /// only offered where it works. See the docs for the recommended registration pattern.
    /// </para>
    /// </summary>
    IMusicAIToolBuilder AddCatalog();

    /// <summary>
    /// Exposes every cross-platform area in one call (library, playback, and playlist management).
    /// Does <b>not</b> include <see cref="AddCatalog"/>, which is Apple-only and must be opted-in explicitly.
    /// </summary>
    IMusicAIToolBuilder AddAll();
}
