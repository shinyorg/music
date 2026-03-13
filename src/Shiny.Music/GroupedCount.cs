namespace Shiny.Music;

/// <summary>
/// Represents a grouped value with its associated track count.
/// </summary>
/// <typeparam name="T">The type of the grouped value (e.g., <see cref="string"/> for genres, <see cref="int"/> for years/decades).</typeparam>
/// <param name="Value">The grouped value.</param>
/// <param name="Count">The number of tracks that belong to this group.</param>
public record GroupedCount<T>(T Value, int Count);
