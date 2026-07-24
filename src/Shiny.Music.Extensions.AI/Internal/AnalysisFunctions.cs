using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI.Internal;

/// <summary>
/// Locates the instrumental sections of a track so an agent can start playback at a specific musical
/// moment (e.g. "the famous guitar solo"). Combines DRM-safe time-synced-lyric gaps with an offline
/// audio-energy analysis, and lets the model bring its own knowledge of the song to pick the right one.
/// </summary>
sealed class AnalyzeSongStructureFunction(IMediaLibrary library, ILyricsProvider? lyrics) : MusicAIFunctionBase(
    "analyze_song_structure",
    "Locate the instrumental sections of a track — intro, breaks, solos, outro — so you can start playback at a " +
    "specific musical moment such as 'the famous guitar solo' or 'the final chorus'. Returns, in seconds: " +
    "'instrumentalGaps' (stretches with no sung lyrics, from time-synced lyrics — available even for DRM tracks) " +
    "and 'audioSections' (loud/quiet energy regions from an offline audio scan; null for DRM-protected tracks). " +
    "Use your own knowledge of the song to choose which section is the one requested, then call play_track with " +
    "start_seconds set to that section's start. No audio is played by this tool.",
    BuildSchema())
{
    static JsonElement BuildSchema()
        => SchemaJson.ToElement(SchemaJson.Object(
            new JsonObject
            {
                ["track_id"] = SchemaJson.String("The track id (from search_tracks / browse_tracks / get_playlist_tracks) to analyze.")
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

        // 1. Lyric-derived instrumental gaps (DRM-safe — no audio decode).
        var gapsArray = new JsonArray();
        var lyricsSynced = false;
        if (lyrics is not null)
        {
            var lyricsResult = await lyrics.GetLyricsAsync(track).ConfigureAwait(false);
            var gaps = lyricsResult.GetInstrumentalGaps(track.Duration);
            lyricsSynced = lyricsResult?.SyncedLyrics is not null;
            foreach (var gap in gaps)
                gapsArray.Add((JsonNode)new JsonObject
                {
                    ["startSeconds"] = Math.Round(gap.Start.TotalSeconds, 1),
                    ["endSeconds"] = Math.Round(gap.End.TotalSeconds, 1),
                    ["durationSeconds"] = Math.Round(gap.Duration.TotalSeconds, 1)
                });
        }

        // 2. Offline audio-energy sections (null for DRM / streaming-only tracks).
        var levels = await library.AnalyzeLevelsAsync(trackId).ConfigureAwait(false);
        JsonNode? sectionsNode = null;
        if (levels is not null)
        {
            var sections = new JsonArray();
            foreach (var section in levels.Sections)
                sections.Add((JsonNode)new JsonObject
                {
                    ["startSeconds"] = Math.Round(section.Start.TotalSeconds, 1),
                    ["endSeconds"] = Math.Round(section.End.TotalSeconds, 1),
                    ["energy"] = section.Energy.ToString().ToLowerInvariant(),
                    ["averageLevel"] = Math.Round(section.AverageLevel, 3)
                });
            sectionsNode = sections;
        }

        return new JsonObject
        {
            ["trackId"] = trackId,
            ["title"] = track.Title,
            ["artist"] = track.Artist,
            ["durationSeconds"] = Math.Round(track.Duration.TotalSeconds, 1),
            ["lyricsSynced"] = lyricsSynced,
            ["instrumentalGaps"] = gapsArray,
            ["audioAnalyzed"] = levels is not null,
            ["audioSections"] = sectionsNode,
            ["hint"] = "Pick the section matching the request from your knowledge of the song, then call play_track with start_seconds set to its start."
        };
    }
}
