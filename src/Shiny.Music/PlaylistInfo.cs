namespace Shiny.Music;

/// <summary>
/// Represents a playlist from the device music library with its track count.
/// </summary>
/// <param name="Id">Platform-specific unique identifier for the playlist. On Android this is the MediaStore playlist row ID; on iOS it is the persistent ID.</param>
/// <param name="Name">The display name of the playlist.</param>
/// <param name="SongCount">The number of tracks in the playlist.</param>
public record PlaylistInfo(string Id, string Name, int SongCount);
