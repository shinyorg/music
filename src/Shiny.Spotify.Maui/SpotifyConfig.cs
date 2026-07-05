namespace Shiny.Spotify.Maui;

/// <summary>
/// Spotify app configuration.
///
/// Setup:
///  1. Create an app at https://developer.spotify.com/dashboard
///  2. Paste the Client ID below (no client secret is needed - we use PKCE)
///  3. Under the app's "Redirect URIs" add EXACTLY: musicsample://spotify-auth
///  4. Add your test users under "User Management" (apps start in dev mode)
///
/// Playback control (play/pause/next) uses the Web API "Player" endpoints which
/// require Spotify Premium AND an active Spotify device (e.g. the Spotify app
/// running on this phone, or any Spotify Connect target).
/// </summary>
public static class SpotifyConfig
{
    public const string ClientId = "YOUR_SPOTIFY_CLIENT_ID";
    public const string RedirectUri = "musicsample://spotify-auth";

    public const string Scopes =
        "user-read-private user-read-email " +
        "playlist-read-private playlist-read-collaborative " +
        "user-read-playback-state user-modify-playback-state streaming";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && ClientId != "YOUR_SPOTIFY_CLIENT_ID";
}
