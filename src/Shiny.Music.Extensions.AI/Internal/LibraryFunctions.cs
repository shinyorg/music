using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI.Internal;

/// <summary>Searches the library by free text against title, artist, and album.</summary>
sealed class SearchTracksFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "search_tracks",
    "Search the device music library for tracks whose title, artist, or album match a free-text query. Returns a compact list of tracks (with their ids, for use with play_track).",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["query"] = SchemaJson.String("Text to match against track title, artist, or album."),
                ["limit"] = SchemaJson.Integer("Maximum number of tracks to return. Defaults to 25, capped at 100.")
            },
            "query"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var query = GetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return new JsonObject { ["error"] = "A non-empty 'query' is required." };

        var limit = Math.Clamp(GetInt(arguments, "limit") ?? 25, 1, 100);
        var results = await library.SearchTracksAsync(query).ConfigureAwait(false);

        var arr = new JsonArray();
        foreach (var t in results.Take(limit))
            arr.Add((JsonNode)TrackJson(t));

        return new JsonObject { ["count"] = arr.Count, ["totalMatches"] = results.Count, ["tracks"] = arr };
    }
}

/// <summary>Browses the library by genre / year / decade (the mood-picking path), optionally with free text.</summary>
sealed class BrowseTracksFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "browse_tracks",
    "Browse the music library filtered by genre, release year, decade, and/or a free-text query (all combined with AND). Use this to pick tracks that fit a mood or era — e.g. genre 'Jazz' for something mellow, or decade 1980 for 80s hits. Returns a compact list of tracks with their ids.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(new JsonObject
        {
            ["genre"] = SchemaJson.String("Genre name to match (case-insensitive). Use list_music_categories with kind 'genres' to discover available genres."),
            ["year"] = SchemaJson.Integer("Exact release year to match. Takes precedence over 'decade'."),
            ["decade"] = SchemaJson.Integer("Decade start year to match, e.g. 1990 for the 1990s. Ignored if 'year' is set."),
            ["query"] = SchemaJson.String("Free-text to match against title, artist, or album."),
            ["limit"] = SchemaJson.Integer("Maximum number of tracks to return. Defaults to 25, capped at 100.")
        }));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var filter = new MusicFilter
        {
            Genre = GetString(arguments, "genre"),
            Year = GetInt(arguments, "year"),
            Decade = GetInt(arguments, "decade"),
            SearchQuery = GetString(arguments, "query")
        };

        var limit = Math.Clamp(GetInt(arguments, "limit") ?? 25, 1, 100);
        var results = await library.GetTracksAsync(filter).ConfigureAwait(false);

        var arr = new JsonArray();
        foreach (var t in results.Take(limit))
            arr.Add((JsonNode)TrackJson(t));

        return new JsonObject { ["count"] = arr.Count, ["totalMatches"] = results.Count, ["tracks"] = arr };
    }
}

/// <summary>Lists the distinct genres / years / decades in the library with track counts.</summary>
sealed class ListCategoriesFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "list_music_categories",
    "List the distinct categories in the music library with a track count for each. Choose a 'kind': genres (names), years, or decades. Optionally narrow with a 'genre' filter (e.g. which decades of Rock exist).",
    BuildSchema())
{
    static readonly string[] Kinds = ["genres", "years", "decades"];

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["kind"] = SchemaJson.String("Which category to list.", Kinds),
                ["genre"] = SchemaJson.String("Optional genre filter to narrow the tracks considered (mainly useful with kind 'years' or 'decades').")
            },
            "kind"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var kind = GetString(arguments, "kind");
        var genre = GetString(arguments, "genre");
        var filter = genre is null ? null : new MusicFilter { Genre = genre };

        var items = new JsonArray();
        switch (kind)
        {
            case "genres":
                foreach (var g in await library.GetGenresAsync(filter).ConfigureAwait(false))
                    items.Add((JsonNode)new JsonObject { ["value"] = g.Value, ["count"] = g.Count });
                break;

            case "years":
                foreach (var y in await library.GetYearsAsync(filter).ConfigureAwait(false))
                    items.Add((JsonNode)new JsonObject { ["value"] = y.Value, ["count"] = y.Count });
                break;

            case "decades":
                foreach (var d in await library.GetDecadesAsync(filter).ConfigureAwait(false))
                    items.Add((JsonNode)new JsonObject { ["value"] = d.Value, ["count"] = d.Count });
                break;

            default:
                return new JsonObject { ["error"] = $"Unknown category kind '{kind}'. Use one of: {string.Join(", ", Kinds)}." };
        }

        return new JsonObject { ["kind"] = kind, ["categories"] = items };
    }
}

/// <summary>Lists all playlists on the device with their track counts.</summary>
sealed class ListPlaylistsFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "list_playlists",
    "List all playlists in the device music library (device playlists and custom locally-stored playlists), each with its id and song count.",
    SchemaJson.ToElement(SchemaJson.Object(new JsonObject())))
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var playlists = await library.GetPlaylistsAsync().ConfigureAwait(false);
        var arr = new JsonArray();
        foreach (var p in playlists)
            arr.Add((JsonNode)new JsonObject { ["id"] = p.Id, ["name"] = p.Name, ["songCount"] = p.SongCount });

        return new JsonObject { ["count"] = arr.Count, ["playlists"] = arr };
    }
}

/// <summary>Gets the tracks contained in a playlist.</summary>
sealed class GetPlaylistTracksFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "get_playlist_tracks",
    "Get the tracks in a playlist, in playlist order. Provide the playlist id from list_playlists.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["playlist_id"] = SchemaJson.String("The playlist id from list_playlists."),
                ["limit"] = SchemaJson.Integer("Maximum number of tracks to return. Defaults to 50, capped at 200.")
            },
            "playlist_id"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var playlistId = GetString(arguments, "playlist_id");
        if (string.IsNullOrWhiteSpace(playlistId))
            return new JsonObject { ["error"] = "A 'playlist_id' is required." };

        var limit = Math.Clamp(GetInt(arguments, "limit") ?? 50, 1, 200);
        var tracks = await library.GetPlaylistTracksAsync(playlistId).ConfigureAwait(false);

        var arr = new JsonArray();
        foreach (var t in tracks.Take(limit))
            arr.Add((JsonNode)TrackJson(t));

        return new JsonObject { ["playlistId"] = playlistId, ["count"] = arr.Count, ["totalTracks"] = tracks.Count, ["tracks"] = arr };
    }
}

/// <summary>Fetches lyrics for a track by id.</summary>
sealed class GetLyricsFunction(IMediaLibrary library, ILyricsProvider lyrics) : MusicAIFunctionBase(
    "get_lyrics",
    "Get the lyrics for a track. Provide the track id from search_tracks / browse_tracks. Returns plain and/or time-synced lyrics when available.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["track_id"] = SchemaJson.String("The track id to fetch lyrics for.")
            },
            "track_id"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var trackId = GetString(arguments, "track_id");
        if (string.IsNullOrWhiteSpace(trackId))
            return new JsonObject { ["error"] = "A 'track_id' is required." };

        var track = await library.GetTrackByIdAsync(trackId).ConfigureAwait(false);
        if (track is null)
            return new JsonObject { ["error"] = $"No track found with id '{trackId}'." };

        var result = await lyrics.GetLyricsAsync(track).ConfigureAwait(false);
        if (result is null || (result.PlainLyrics is null && result.SyncedLyrics is null))
            return new JsonObject { ["trackId"] = trackId, ["found"] = false };

        return new JsonObject
        {
            ["trackId"] = trackId,
            ["found"] = true,
            ["plainLyrics"] = result.PlainLyrics,
            ["syncedLyrics"] = result.SyncedLyrics
        };
    }
}
