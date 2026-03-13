namespace Shiny.Music;

/// <summary>
/// Defines optional criteria for filtering music tracks. All specified properties are combined with AND logic.
/// </summary>
public class MusicFilter
{
    /// <summary>
    /// Filters tracks by genre name (case-insensitive match).
    /// </summary>
    public string? Genre { get; init; }

    /// <summary>
    /// Filters tracks by exact release year. Takes precedence over <see cref="Decade"/> if both are set.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Filters tracks by decade, specified as the starting year (e.g., 1990 for the 1990s).
    /// Ignored if <see cref="Year"/> is also set.
    /// </summary>
    public int? Decade { get; init; }

    /// <summary>
    /// Filters tracks by a text search query matched against title, artist, or album (case-insensitive, contains match).
    /// </summary>
    public string? SearchQuery { get; init; }
}
