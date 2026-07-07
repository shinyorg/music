using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI.Internal;

/// <summary>Creates a new custom (locally-stored) playlist.</summary>
sealed class CreatePlaylistFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "create_playlist",
    "Create a new custom (locally-stored) playlist with the given name. Returns the new playlist id for use with modify_playlist.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["name"] = SchemaJson.String("The display name for the new playlist.")
            },
            "name"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var name = GetString(arguments, "name");
        if (string.IsNullOrWhiteSpace(name))
            return new JsonObject { ["error"] = "A non-empty 'name' is required." };

        var playlist = await library.CreatePlaylistAsync(name).ConfigureAwait(false);
        return new JsonObject
        {
            ["id"] = playlist.Id,
            ["name"] = playlist.Name,
            ["songCount"] = playlist.SongCount
        };
    }
}

/// <summary>Adds or removes a track in a custom playlist.</summary>
sealed class ModifyPlaylistFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "modify_playlist",
    "Add or remove a track in a custom playlist. Provide the custom playlist id (from create_playlist or list_playlists) and the track id.",
    BuildSchema())
{
    static readonly string[] Actions = ["add_track", "remove_track"];

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["action"] = SchemaJson.String("Whether to add or remove the track.", Actions),
                ["playlist_id"] = SchemaJson.String("The custom playlist id."),
                ["track_id"] = SchemaJson.String("The id of the track to add or remove.")
            },
            "action", "playlist_id", "track_id"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var action = GetString(arguments, "action");
        var playlistId = GetString(arguments, "playlist_id");
        var trackId = GetString(arguments, "track_id");

        if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(trackId))
            return new JsonObject { ["error"] = "'playlist_id' and 'track_id' are required." };

        switch (action)
        {
            case "add_track":
                var track = await library.GetTrackByIdAsync(trackId).ConfigureAwait(false);
                if (track is null)
                    return new JsonObject { ["error"] = $"No track found with id '{trackId}'." };
                await library.AddTrackToPlaylistAsync(playlistId, track).ConfigureAwait(false);
                break;

            case "remove_track":
                await library.RemoveTrackFromPlaylistAsync(playlistId, trackId).ConfigureAwait(false);
                break;

            default:
                return new JsonObject { ["error"] = $"Unknown action '{action}'. Use one of: {string.Join(", ", Actions)}." };
        }

        var updated = await library.GetPlaylistByIdAsync(playlistId).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = "ok",
            ["action"] = action,
            ["playlistId"] = playlistId,
            ["songCount"] = updated?.SongCount
        };
    }
}

/// <summary>Deletes a custom playlist.</summary>
sealed class DeletePlaylistFunction(IMediaLibrary library) : MusicAIFunctionBase(
    "delete_playlist",
    "Delete a custom (locally-stored) playlist by its id.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["playlist_id"] = SchemaJson.String("The custom playlist id to delete.")
            },
            "playlist_id"));

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var playlistId = GetString(arguments, "playlist_id");
        if (string.IsNullOrWhiteSpace(playlistId))
            return new JsonObject { ["error"] = "A 'playlist_id' is required." };

        await library.RemovePlaylistAsync(playlistId).ConfigureAwait(false);
        return new JsonObject { ["status"] = "deleted", ["playlistId"] = playlistId };
    }
}
