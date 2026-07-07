using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI.Internal;

/// <summary>Loads and plays a track by id.</summary>
sealed class PlayTrackFunction(IMediaLibrary library, IMusicPlayer player) : MusicAIFunctionBase(
    "play_track",
    "Load and start playing a track by its id (from search_tracks / browse_tracks / get_playlist_tracks). Any currently playing track is stopped first.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["track_id"] = SchemaJson.String("The id of the track to play.")
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
        if (!track.IsPlayable)
            return new JsonObject { ["error"] = $"Track '{trackId}' is not playable (e.g. DRM-protected streaming content)." };

        try
        {
            await player.PlayAsync(track).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return new JsonObject { ["error"] = $"Playback failed: {ex.Message}" };
        }

        return new JsonObject
        {
            ["status"] = "playing",
            ["track"] = TrackJson(track)
        };
    }
}

/// <summary>Pause / resume / stop / seek the current playback.</summary>
sealed class ControlPlaybackFunction(IMusicPlayer player) : MusicAIFunctionBase(
    "control_playback",
    "Control the current playback: pause, resume, stop, or seek. For 'seek', also supply position_seconds.",
    BuildSchema())
{
    static readonly string[] Actions = ["pause", "resume", "stop", "seek"];

    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["action"] = SchemaJson.String("The playback action to perform.", Actions),
                ["position_seconds"] = SchemaJson.Number("Target position in seconds. Required when action is 'seek'.")
            },
            "action"));

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var action = GetString(arguments, "action");
        switch (action)
        {
            case "pause":
                player.Pause();
                break;

            case "resume":
                player.Resume();
                break;

            case "stop":
                player.Stop();
                break;

            case "seek":
                var seconds = GetDouble(arguments, "position_seconds");
                if (seconds is null || seconds < 0)
                    return new ValueTask<object?>(new JsonObject { ["error"] = "A non-negative 'position_seconds' is required for 'seek'." });
                player.Seek(TimeSpan.FromSeconds(seconds.Value));
                break;

            default:
                return new ValueTask<object?>(new JsonObject { ["error"] = $"Unknown action '{action}'. Use one of: {string.Join(", ", Actions)}." });
        }

        return new ValueTask<object?>(NowPlayingJson(player));
    }

    internal static JsonObject NowPlayingJson(IMusicPlayer player)
    {
        var o = new JsonObject
        {
            ["state"] = player.State.ToString().ToLowerInvariant(),
            ["positionSeconds"] = (int)player.Position.TotalSeconds,
            ["durationSeconds"] = (int)player.Duration.TotalSeconds,
            ["isDucked"] = player.IsDucked,
            ["track"] = player.CurrentTrack is null ? null : TrackJson(player.CurrentTrack)
        };
        return o;
    }
}

/// <summary>Reports the current "now playing" status.</summary>
sealed class GetNowPlayingFunction(IMusicPlayer player) : MusicAIFunctionBase(
    "get_now_playing",
    "Get the current playback status: state (stopped/playing/paused), the current track (if any), position, and duration.",
    SchemaJson.ToElement(SchemaJson.Object(new JsonObject())))
{
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => new(ControlPlaybackFunction.NowPlayingJson(player));
}
