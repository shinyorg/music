using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Shiny.Spotify.Maui.Infrastructure;

/// <summary>
/// Typed wrapper over the Spotify Web API covering the things the sample needs:
/// authentication (OAuth Authorization Code + PKCE), search, the user's playlists,
/// and playback control. Tokens are persisted in <see cref="SecureStorage"/> and
/// refreshed automatically when expired.
/// JSON uses the <see cref="SpotifyJsonContext"/> source generator (no reflection) so the
/// library stays trim/AOT-safe.
/// </summary>
public class SpotifyClient : ISpotifyClient
{
    const string ApiBase = "https://api.spotify.com/v1";
    const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    const string TokenUrl = "https://accounts.spotify.com/api/token";

    const string KeyAccess = "spotify.access_token";
    const string KeyRefresh = "spotify.refresh_token";
    const string KeyExpiry = "spotify.expiry";

    readonly HttpClient http = new();

    string? accessToken;
    string? refreshToken;
    DateTimeOffset expiresAt;

    // ── Authentication (OAuth Authorization Code + PKCE) ─────────────────────────

    public bool IsAuthenticated => !string.IsNullOrEmpty(refreshToken);

    /// <summary>Loads any previously saved tokens. Call once at startup.</summary>
    public async Task RestoreAsync()
    {
        accessToken = await SecureStorage.GetAsync(KeyAccess);
        refreshToken = await SecureStorage.GetAsync(KeyRefresh);
        var expiryRaw = await SecureStorage.GetAsync(KeyExpiry);
        if (long.TryParse(expiryRaw, out var unix))
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix);
    }

    /// <summary>Runs the interactive login flow in a system web view.</summary>
    public async Task LoginAsync()
    {
        var verifier = GenerateCodeVerifier();
        var challenge = GenerateCodeChallenge(verifier);
        var state = GenerateCodeVerifier()[..16];

        var authUrl =
            $"{AuthorizeUrl}?client_id={SpotifyConfig.ClientId}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(SpotifyConfig.RedirectUri)}" +
            "&code_challenge_method=S256" +
            $"&code_challenge={challenge}" +
            $"&state={state}" +
            $"&scope={Uri.EscapeDataString(SpotifyConfig.Scopes)}";

        var result = await WebAuthenticator.Default.AuthenticateAsync(
            new WebAuthenticatorOptions
            {
                Url = new Uri(authUrl),
                CallbackUrl = new Uri(SpotifyConfig.RedirectUri),
                PrefersEphemeralWebBrowserSession = false
            });

        if (result.Properties.TryGetValue("error", out var error))
            throw new InvalidOperationException($"Spotify login failed: {error}");

        if (!result.Properties.TryGetValue("code", out var code))
            throw new InvalidOperationException("Spotify did not return an authorization code.");

        if (result.Properties.TryGetValue("state", out var returnedState) && returnedState != state)
            throw new InvalidOperationException("Spotify state mismatch - possible CSRF.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = SpotifyConfig.RedirectUri,
            ["client_id"] = SpotifyConfig.ClientId,
            ["code_verifier"] = verifier
        };
        await RequestTokenAsync(form);
    }

    public async Task LogoutAsync()
    {
        accessToken = refreshToken = null;
        expiresAt = default;
        SecureStorage.Remove(KeyAccess);
        SecureStorage.Remove(KeyRefresh);
        SecureStorage.Remove(KeyExpiry);
        await Task.CompletedTask;
    }

    /// <summary>Returns a valid access token, refreshing it first if needed.</summary>
    async Task<string> GetAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(refreshToken))
            throw new InvalidOperationException("Not authenticated with Spotify.");

        // refresh a minute early to avoid edge-of-expiry failures
        if (string.IsNullOrEmpty(accessToken) || DateTimeOffset.UtcNow >= expiresAt.AddMinutes(-1))
            await RefreshAsync();

        return accessToken!;
    }

    async Task RefreshAsync()
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken!,
            ["client_id"] = SpotifyConfig.ClientId
        };
        await RequestTokenAsync(form);
    }

    async Task RequestTokenAsync(Dictionary<string, string> form)
    {
        using var resp = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Spotify token request failed ({(int)resp.StatusCode}): {json}");

        var token = JsonSerializer.Deserialize(json, SpotifyJsonContext.Default.TokenResponse)
                    ?? throw new InvalidOperationException("Empty token response from Spotify.");

        accessToken = token.AccessToken;
        expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
        if (!string.IsNullOrEmpty(token.RefreshToken))
            refreshToken = token.RefreshToken; // refresh flow may omit a new one

        await SecureStorage.SetAsync(KeyAccess, accessToken);
        await SecureStorage.SetAsync(KeyExpiry, expiresAt.ToUnixTimeSeconds().ToString());
        if (!string.IsNullOrEmpty(refreshToken))
            await SecureStorage.SetAsync(KeyRefresh, refreshToken);
    }

    // ── Search ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SpotifyTrack>> SearchTracksAsync(string query, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var url = $"{ApiBase}/search?type=track&limit={limit}&q={Uri.EscapeDataString(query)}";
        var resp = await SendAsync(HttpMethod.Get, url);
        var body = await resp.Content.ReadFromJsonAsync(SpotifyJsonContext.Default.SearchResponse);
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
            var page = await resp.Content.ReadFromJsonAsync(SpotifyJsonContext.Default.PagedResponseApiPlaylist);
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
            var page = await resp.Content.ReadFromJsonAsync(SpotifyJsonContext.Default.PagedResponsePlaylistItem);
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
        var body = await resp.Content.ReadFromJsonAsync(SpotifyJsonContext.Default.DevicesResponse);
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

        var payload = JsonSerializer.Serialize(
            new PlayRequest { Uris = [trackUri] }, SpotifyJsonContext.Default.PlayRequest);
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
        var payload = JsonSerializer.Serialize(
            new TransferRequest { DeviceIds = [deviceId], Play = false }, SpotifyJsonContext.Default.TransferRequest);
        return SendVoidAsync(HttpMethod.Put, $"{ApiBase}/me/player",
            new StringContent(payload, Encoding.UTF8, "application/json"));
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────

    async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var token = await GetAccessTokenAsync();
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

    // ── PKCE helpers ─────────────────────────────────────────────────────────

    static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    static string GenerateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
