using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI.Internal;

static class MusicAIFunctionFactory
{
    public static IReadOnlyList<AITool> Build(
        IMediaLibrary library,
        IMusicPlayer? player,
        ILyricsProvider? lyrics,
        MusicAIToolBuilder builder)
    {
        var tools = new List<AITool>();

        // Shared bridge so play_track can stream catalog results (which aren't in the local library).
        // Populated by search_catalog (when AddCatalog is enabled); harmless and empty otherwise.
        var catalogCache = new CatalogTrackCache();

        if (builder.Library)
        {
            tools.Add(new SearchTracksFunction(library));
            tools.Add(new BrowseTracksFunction(library));
            tools.Add(new ListCategoriesFunction(library));
            tools.Add(new ListPlaylistsFunction(library));
            tools.Add(new GetPlaylistTracksFunction(library));
            if (lyrics is not null)
                tools.Add(new GetLyricsFunction(library, lyrics));
        }

        if (builder.Catalog)
            tools.Add(new SearchCatalogFunction(library, catalogCache));

        if (builder.Playback)
        {
            if (player is null)
                throw new InvalidOperationException(
                    "AddPlayback() was requested but no IMusicPlayer is registered. " +
                    "Call AddShinyMusic() on a platform target (Android / iOS / Mac Catalyst) before AddMusicAITools().");

            tools.Add(new PlayTrackFunction(library, player, catalogCache));
            tools.Add(new ControlPlaybackFunction(player));
            tools.Add(new GetNowPlayingFunction(player));
        }

        if (builder.PlaylistManagement)
        {
            tools.Add(new CreatePlaylistFunction(library));
            tools.Add(new ModifyPlaylistFunction(library));
            tools.Add(new DeletePlaylistFunction(library));
        }

        return tools;
    }
}
