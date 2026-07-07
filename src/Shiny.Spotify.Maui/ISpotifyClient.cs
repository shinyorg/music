namespace Shiny.Spotify.Maui;

/// <summary>
/// Typed wrapper over the Spotify Web API: authentication (OAuth PKCE), search, the user's
/// playlists, and playback control. Abstracted so it can be mocked/faked in unit tests.
/// </summary>
public interface ISpotifyClient
{
    // ── Authentication (OAuth Authorization Code + PKCE) ─────────────────────────

    /// <summary>Whether a Spotify session is available (a refresh token is held).</summary>
    bool IsAuthenticated { get; }

    /// <summary>Loads any previously saved tokens from secure storage. Call once at startup.</summary>
    Task RestoreAsync();

    /// <summary>Runs the interactive login flow in a system web view and persists the tokens.</summary>
    Task LoginAsync();

    /// <summary>Clears the current session and removes persisted tokens.</summary>
    Task LogoutAsync();

    // ── Catalog & Playback ───────────────────────────────────────────────────────

    /// <summary>Searches the Spotify catalog for tracks matching <paramref name="query"/>.</summary>
    Task<IReadOnlyList<SpotifyTrack>> SearchTracksAsync(string query, int limit = 25);

    /// <summary>Gets the current user's playlists (all pages).</summary>
    Task<IReadOnlyList<SpotifyPlaylist>> GetPlaylistsAsync();

    /// <summary>Gets the tracks in the given playlist (all pages).</summary>
    Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId);

    /// <summary>Gets the user's available Spotify Connect devices.</summary>
    Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync();

    /// <summary>Starts playback of a track (optionally on a specific device). Requires Premium + an active device.</summary>
    Task PlayTrackAsync(string trackUri, string? deviceId = null);

    /// <summary>Pauses playback.</summary>
    Task PauseAsync();

    /// <summary>Resumes playback.</summary>
    Task ResumeAsync();

    /// <summary>Skips to the next track.</summary>
    Task NextAsync();

    /// <summary>Skips to the previous track.</summary>
    Task PreviousAsync();

    /// <summary>Moves playback to the given device (e.g. the Spotify app on this phone).</summary>
    Task TransferPlaybackAsync(string deviceId);
}
