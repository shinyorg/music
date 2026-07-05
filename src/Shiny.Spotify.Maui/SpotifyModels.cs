using System.Text.Json.Serialization;

namespace Shiny.Spotify.Maui;

// ── View-facing models (clean, mapped from the raw API DTOs) ─────────────────

public record SpotifyTrack(
    string Id,
    string Uri,
    string Title,
    string Artist,
    string Album,
    string? ImageUrl,
    int DurationMs
)
{
    public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);
}

public record SpotifyPlaylist(
    string Id,
    string Uri,
    string Name,
    string Owner,
    int TrackCount,
    string? ImageUrl
);

public record SpotifyDevice(
    string Id,
    string Name,
    string Type,
    bool IsActive
);

// ── Raw API DTOs (System.Text.Json) ─────────────────────────────────────────

class TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("scope")] public string? Scope { get; set; }
}

class PagedResponse<T>
{
    [JsonPropertyName("items")] public List<T> Items { get; set; } = [];
    [JsonPropertyName("next")] public string? Next { get; set; }
}

class SearchResponse
{
    [JsonPropertyName("tracks")] public PagedResponse<ApiTrack> Tracks { get; set; } = new();
}

class ApiTrack
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("duration_ms")] public int DurationMs { get; set; }
    [JsonPropertyName("artists")] public List<ApiArtist> Artists { get; set; } = [];
    [JsonPropertyName("album")] public ApiAlbum? Album { get; set; }
}

class ApiArtist
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

class ApiAlbum
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("images")] public List<ApiImage> Images { get; set; } = [];
}

class ApiImage
{
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("width")] public int? Width { get; set; }
}

class ApiPlaylist
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("uri")] public string Uri { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("owner")] public ApiOwner? Owner { get; set; }
    [JsonPropertyName("tracks")] public ApiTrackRef? Tracks { get; set; }
    [JsonPropertyName("images")] public List<ApiImage> Images { get; set; } = [];
}

class ApiOwner
{
    [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
}

class ApiTrackRef
{
    [JsonPropertyName("total")] public int Total { get; set; }
}

class PlaylistItem
{
    [JsonPropertyName("track")] public ApiTrack? Track { get; set; }
}

class DevicesResponse
{
    [JsonPropertyName("devices")] public List<ApiDevice> Devices { get; set; } = [];
}

// ── Request bodies (serialized to Spotify) ───────────────────────────────────

class PlayRequest
{
    [JsonPropertyName("uris")] public string[] Uris { get; set; } = [];
}

class TransferRequest
{
    [JsonPropertyName("device_ids")] public string[] DeviceIds { get; set; } = [];
    [JsonPropertyName("play")] public bool Play { get; set; }
}

class ApiDevice
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("is_active")] public bool IsActive { get; set; }
}
