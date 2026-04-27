namespace Shiny.Music;

/// <summary>
/// Represents the result of identifying a song from audio.
/// </summary>
/// <param name="Title">The title of the identified track.</param>
/// <param name="Artist">The artist or performer, or <c>null</c> if not available.</param>
/// <param name="Album">The album name, or <c>null</c> if not available.</param>
/// <param name="Genre">The genre, or <c>null</c> if not available.</param>
/// <param name="ArtworkUrl">A URL pointing to album or track artwork, or <c>null</c> if not available.</param>
/// <param name="MusicUrl">A URL to the track on a music streaming service (e.g. Apple Music, YouTube Music), or <c>null</c> if not available.</param>
/// <param name="Isrc">The International Standard Recording Code for the track, or <c>null</c> if not available.</param>
public record MusicIdentificationResult(
    string Title,
    string? Artist,
    string? Album,
    string? Genre,
    string? ArtworkUrl,
    string? MusicUrl,
    string? Isrc
);
