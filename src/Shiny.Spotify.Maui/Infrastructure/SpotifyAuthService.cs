using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Shiny.Spotify.Maui.Infrastructure;

/// <summary>
/// Handles Spotify OAuth using the Authorization Code + PKCE flow (no client
/// secret, safe for mobile). Tokens are persisted in <see cref="SecureStorage"/>
/// and refreshed automatically when expired.
/// </summary>
public class SpotifyAuthService
{
    const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    const string TokenUrl = "https://accounts.spotify.com/api/token";

    const string KeyAccess = "spotify.access_token";
    const string KeyRefresh = "spotify.refresh_token";
    const string KeyExpiry = "spotify.expiry";

    readonly HttpClient http = new();

    string? accessToken;
    string? refreshToken;
    DateTimeOffset expiresAt;

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
    public async Task<string> GetAccessTokenAsync()
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
