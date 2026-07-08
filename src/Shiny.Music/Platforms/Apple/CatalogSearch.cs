using System.Text.Json.Serialization;

namespace Shiny.Music;

/// <summary>
/// DTO mirroring the JSON emitted by the MusicKit binding's <c>searchCatalog</c> for one catalog song.
/// Mapped to <see cref="MusicMetadata"/> by the Apple <c>MediaLibrary</c>.
/// </summary>
class CatalogSong
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("album")]
    public string? Album { get; set; }

    [JsonPropertyName("genre")]
    public string? Genre { get; set; }

    [JsonPropertyName("durationMillis")]
    public long DurationMillis { get; set; }

    [JsonPropertyName("isExplicit")]
    public bool IsExplicit { get; set; }

    [JsonPropertyName("artworkUrl")]
    public string? ArtworkUrl { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }
}

[JsonSerializable(typeof(List<CatalogSong>))]
partial class CatalogJsonContext : JsonSerializerContext;
