using System.Collections.Concurrent;

namespace Shiny.Music.Extensions.AI.Internal;

/// <summary>
/// Bridges catalog search results to playback. Catalog tracks are not in the user's local library,
/// so <c>play_track</c> cannot re-resolve them via <see cref="IMediaLibrary.GetTrackByIdAsync"/>.
/// <c>search_catalog</c> stashes each result here (keyed by <see cref="MusicMetadata.CatalogId"/>)
/// so <c>play_track</c> can find and stream it by id. Bounded to avoid unbounded growth in a
/// long-lived process; the model can always re-run the search if an entry has been evicted.
/// </summary>
sealed class CatalogTrackCache
{
    const int MaxEntries = 512;
    readonly ConcurrentDictionary<string, MusicMetadata> tracks = new();

    public void Remember(MusicMetadata track)
    {
        if (string.IsNullOrEmpty(track.CatalogId))
            return;

        if (this.tracks.Count >= MaxEntries)
            this.tracks.Clear();

        this.tracks[track.CatalogId] = track;
    }

    public MusicMetadata? Get(string id)
        => this.tracks.TryGetValue(id, out var track) ? track : null;
}
