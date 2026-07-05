using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Shiny.Spotify.Maui;

/// <summary>
/// Thin typed wrapper over the Spotify Web API covering the three things the
/// sample needs: search, the user's playlists, and playback control.
/// </summary>
public class SpotifyClient(SpotifyAuthService auth)
{
    const string ApiBase = "https://api.spotify.com/v1";
    readonly HttpClient http = new();

    // ── Search ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SpotifyTrack>> SearchTracksAsync(string query, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var url = $"{ApiBase}/search?type=track&limit={limit}&q={Uri.EscapeDataString(query)}";
        var resp = await SendAsync(HttpMethod.Get, url);
        var body = await resp.Content.ReadFromJsonAsync<SearchResponse>();
        return body?.Tracks.Items.Select(MapTrack).ToList() ?? [];
    }

    // ── Playlists ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SpotifyPlaylist>> GetPlaylistsAsync()
    {
        var results = new List<SpotifyPlaylist>();
        var url = $"{ApiBase}/me/playlists?limit=50";

        while (url != null)
        {
            var resp = await SendAsync(HttpMethod.Get, url);
            var page = await resp.Content.ReadFromJsonAsync<PagedResponse<ApiPlaylist>>();
            if (page == null)
                break;

            results.AddRange(page.Items
                .Where(p => p != null)
                .Select(p => new SpotifyPlaylist(
                    p.Id,
                    p.Uri,
                    p.Name,
                    p.Owner?.DisplayName ?? "",
                    p.Tracks?.Total ?? 0,
                    PickImage(p.Images))));

            url = page.Next;
        }
        return results;
    }

    public async Task<IReadOnlyList<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId)
    {
        var results = new List<SpotifyTrack>();
        var url = $"{ApiBase}/playlists/{playlistId}/tracks?limit=100";

        while (url != null)
        {
            var resp = await SendAsync(HttpMethod.Get, url);
            var page = await resp.Content.ReadFromJsonAsync<PagedResponse<PlaylistItem>>();
            if (page == null)
                break;

            results.AddRange(page.Items
                .Where(i => i.Track != null)
                .Select(i => MapTrack(i.Track!)));

            url = page.Next;
        }
        return results;
    }

    // ── Playback (requires Premium + an active device) ──────────────────────────

    public async Task<IReadOnlyList<SpotifyDevice>> GetDevicesAsync()
    {
        var resp = await SendAsync(HttpMethod.Get, $"{ApiBase}/me/player/devices");
        var body = await resp.Content.ReadFromJsonAsync<DevicesResponse>();
        return body?.Devices
            .Where(d => d.Id != null)
            .Select(d => new SpotifyDevice(d.Id!, d.Name, d.Type, d.IsActive))
            .ToList() ?? [];
    }

    /// <summary>Starts playback of a track (optionally on a specific device).</summary>
    public async Task PlayTrackAsync(string trackUri, string? deviceId = null)
    {
        var url = $"{ApiBase}/me/player/play";
        if (deviceId != null)
            url += $"?device_id={deviceId}";

        var payload = JsonSerializer.Serialize(new { uris = new[] { trackUri } });
        await SendAsync(HttpMethod.Put, url,
            new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    public Task PauseAsync() => SendVoidAsync(HttpMethod.Put, $"{ApiBase}/me/player/pause");
    public Task ResumeAsync() => SendVoidAsync(HttpMethod.Put, $"{ApiBase}/me/player/play");
    public Task NextAsync() => SendVoidAsync(HttpMethod.Post, $"{ApiBase}/me/player/next");
    public Task PreviousAsync() => SendVoidAsync(HttpMethod.Post, $"{ApiBase}/me/player/previous");

    /// <summary>Moves playback to the given device (e.g. the Spotify app on this phone).</summary>
    public Task TransferPlaybackAsync(string deviceId)
    {
        var payload = JsonSerializer.Serialize(new { device_ids = new[] { deviceId }, play = false });
        return SendVoidAsync(HttpMethod.Put, $"{ApiBase}/me/player",
            new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var token = await auth.GetAccessTokenAsync();
        using var req = new HttpRequestMessage(method, url) { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync();
            throw new SpotifyApiException((int)resp.StatusCode, detail);
        }
        return resp;
    }

    async Task SendVoidAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var resp = await SendAsync(method, url, content);
        resp.Dispose();
    }

    static SpotifyTrack MapTrack(ApiTrack t) => new(
        t.Id,
        t.Uri,
        t.Name,
        string.Join(", ", t.Artists.Select(a => a.Name)),
        t.Album?.Name ?? "",
        PickImage(t.Album?.Images),
        t.DurationMs);

    static string? PickImage(List<ApiImage>? images)
    {
        if (images == null || images.Count == 0)
            return null;

        // prefer a mid-size image (~300px) over the giant hero art
        return images
            .OrderBy(i => Math.Abs((i.Width ?? 640) - 300))
            .First().Url;
    }
}

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
