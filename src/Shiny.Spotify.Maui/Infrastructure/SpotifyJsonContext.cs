using System.Text.Json.Serialization;

namespace Shiny.Spotify.Maui.Infrastructure;

/// <summary>
/// System.Text.Json source-generation context for the Spotify DTOs. Using generated
/// (reflection-free) metadata keeps serialization trim- and AOT-safe. Register every
/// root type that is directly (de)serialized; nested types are pulled in automatically.
/// </summary>
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(PagedResponse<ApiPlaylist>))]
[JsonSerializable(typeof(PagedResponse<PlaylistItem>))]
[JsonSerializable(typeof(DevicesResponse))]
[JsonSerializable(typeof(PlayRequest))]
[JsonSerializable(typeof(TransferRequest))]
internal partial class SpotifyJsonContext : JsonSerializerContext;
