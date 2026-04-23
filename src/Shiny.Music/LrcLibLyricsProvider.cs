using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Shiny.Music;

public class LrcLibLyricsProvider(HttpClient httpClient) : ILyricsProvider
{
    public async Task<LyricsResult?> GetLyricsAsync(MusicMetadata track)
    {
        if (string.IsNullOrWhiteSpace(track.Artist) && string.IsNullOrWhiteSpace(track.Title))
            return null;

        try
        {
            var artist = Uri.EscapeDataString(track.Artist ?? string.Empty);
            var title = Uri.EscapeDataString(track.Title ?? string.Empty);
            var duration = (int)track.Duration.TotalSeconds;

            var url = $"https://lrclib.net/api/get?artist_name={artist}&track_name={title}&duration={duration}";
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content
                .ReadFromJsonAsync(LrcLibJsonContext.Default.LrcLibResponse)
                .ConfigureAwait(false);

            if (result == null)
                return null;

            if (string.IsNullOrWhiteSpace(result.PlainLyrics) && string.IsNullOrWhiteSpace(result.SyncedLyrics))
                return null;

            return new LyricsResult(result.PlainLyrics, result.SyncedLyrics);
        }
        catch
        {
            return null;
        }
    }
}

internal class LrcLibResponse
{
    [JsonPropertyName("plainLyrics")]
    public string? PlainLyrics { get; set; }

    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }
}

[JsonSerializable(typeof(LrcLibResponse))]
internal partial class LrcLibJsonContext : JsonSerializerContext;
