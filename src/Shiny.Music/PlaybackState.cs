namespace Shiny.Music;

/// <summary>
/// Represents the current state of the music player.
/// </summary>
public enum PlaybackState
{
    /// <summary>No track is playing. The player is idle or a track has finished.</summary>
    Stopped,

    /// <summary>A track is currently playing.</summary>
    Playing,

    /// <summary>Playback is paused and can be resumed.</summary>
    Paused
}
