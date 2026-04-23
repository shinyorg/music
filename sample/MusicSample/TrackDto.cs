using System.Text.Json.Serialization;

namespace MusicSample;

public record TrackDto(
    string Id,
    string? Title,
    string? Artist,
    string? Album,
    string? Genre,
    TimeSpan Duration,
    string? AlbumArtUri,
    bool? IsExplicit,
    string ContentUri,
    string? StoreId,
    int? Year
);

[JsonSerializable(typeof(List<TrackDto>))]
internal partial class TrackDtoJsonContext : JsonSerializerContext;
