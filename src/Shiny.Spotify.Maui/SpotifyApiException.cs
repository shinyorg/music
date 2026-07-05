namespace Shiny.Spotify.Maui;

/// <summary>
/// Thrown when a Spotify Web API request returns a non-success status code.
/// </summary>
public class SpotifyApiException(int statusCode, string detail)
    : Exception(BuildMessage(statusCode, detail))
{
    public int StatusCode { get; } = statusCode;

    static string BuildMessage(int status, string detail) => status switch
    {
        401 => "Spotify session expired. Please sign in again.",
        403 => "Spotify Premium is required for playback control.",
        404 => "No active Spotify device found. Open Spotify and start playing something, then try again.",
        429 => "Spotify rate limit hit. Please wait a moment.",
        _ => $"Spotify API error ({status}): {detail}"
    };
}
