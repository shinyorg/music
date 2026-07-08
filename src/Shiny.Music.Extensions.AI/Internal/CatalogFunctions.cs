using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI.Internal;

/// <summary>
/// Searches the Apple Music streaming catalog (Apple platforms only). Results are stashed in the
/// shared <see cref="CatalogTrackCache"/> so <c>play_track</c> can stream them by id.
/// </summary>
sealed class SearchCatalogFunction(IMediaLibrary library, CatalogTrackCache cache) : MusicAIFunctionBase(
    "search_catalog",
    "Search the Apple Music streaming catalog for songs — including tracks that are NOT in the user's local library. Requires an active Apple Music subscription and only works on Apple platforms (iOS / Mac Catalyst). Returns a compact list of tracks with ids usable with play_track. Prefer search_tracks to search only the on-device library.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["query"] = SchemaJson.String("Text to match against catalog song title, artist, or album."),
                ["limit"] = SchemaJson.Integer("Maximum number of tracks to return. Defaults to 25; Apple Music caps this at 25.")
            },
            "query"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var query = GetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return new JsonObject { ["error"] = "A non-empty 'query' is required." };

        var limit = Math.Clamp(GetInt(arguments, "limit") ?? 25, 1, 25);

        IReadOnlyList<MusicMetadata> results;
        try
        {
            results = await library.SearchCatalogAsync(query, limit).ConfigureAwait(false);
        }
        catch (PlatformNotSupportedException)
        {
            return new JsonObject { ["error"] = "Catalog search is only available on Apple platforms (iOS / Mac Catalyst)." };
        }

        var arr = new JsonArray();
        foreach (var t in results)
        {
            cache.Remember(t);
            arr.Add((JsonNode)TrackJson(t));
        }

        var response = new JsonObject { ["count"] = arr.Count, ["tracks"] = arr };
        if (arr.Count == 0)
            response["note"] = "No results — the term matched nothing, or the user has no active Apple Music subscription / has not authorized MusicKit.";

        return response;
    }
}
