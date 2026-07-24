using System.Runtime.InteropServices;
using System.Text.Json;
using AudioToolbox;
using AVFoundation;
using Foundation;
using MediaPlayer;
using Shiny.Music.Internal;
using ShinyMusicKit;
using UIKit;

namespace Shiny.Music;

public class MediaLibrary : IMediaLibrary
{
    readonly CustomPlaylistStore customPlaylists = new();

    public Task<PermissionStatus> CheckPermissionAsync()
        => Task.FromResult(ToPermissionStatus(MPMediaLibrary.AuthorizationStatus));

    public Task<PermissionStatus> RequestPermissionAsync()
    {
        var current = MPMediaLibrary.AuthorizationStatus;
        if (current == MPMediaLibraryAuthorizationStatus.Authorized)
            return Task.FromResult(PermissionStatus.Granted);

        var tcs = new TaskCompletionSource<PermissionStatus>();
        MPMediaLibrary.RequestAuthorization(status => tcs.SetResult(ToPermissionStatus(status)));
        return tcs.Task;
    }

    public Task<IReadOnlyList<MusicMetadata>> GetAllTracksAsync()
    {
        return Task.Run(() =>
        {
            var query = MPMediaQuery.SongsQuery;
            return (IReadOnlyList<MusicMetadata>)(query.Items?
                .Select(ToMusicMetadata)
                .ToList()
                .AsReadOnly() ?? new List<MusicMetadata>().AsReadOnly());
        });
    }

    public Task<IReadOnlyList<MusicMetadata>> SearchTracksAsync(string searchQuery)
    {
        return Task.Run(() =>
        {
            var query = MPMediaQuery.SongsQuery;
            var items = query.Items ?? [];
            return (IReadOnlyList<MusicMetadata>)items
                .Where(i =>
                    ContainsIgnoreCase(i.Title, searchQuery) ||
                    ContainsIgnoreCase(i.Artist, searchQuery) ||
                    ContainsIgnoreCase(i.AlbumTitle, searchQuery))
                .Select(ToMusicMetadata)
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<IReadOnlyList<MusicMetadata>> GetTracksAsync(MusicFilter filter)
    {
        return Task.Run(() =>
        {
            var items = GetFilteredItems(filter);
            return (IReadOnlyList<MusicMetadata>)items
                .Select(ToMusicMetadata)
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<MusicMetadata?> GetTrackByIdAsync(string trackId)
    {
        return Task.Run(() =>
        {
            if (!ulong.TryParse(trackId, out var pid))
                return (MusicMetadata?)null;

            var query = MPMediaQuery.SongsQuery;
            var item = query.Items?.FirstOrDefault(i => i.PersistentID == pid);
            return item == null ? (MusicMetadata?)null : ToMusicMetadata(item);
        });
    }

    public Task<IReadOnlyList<MusicMetadata>> GetTracksByIdsAsync(IEnumerable<string> trackIds)
    {
        var pidOrder = trackIds
            .Select(t => ulong.TryParse(t, out var v) ? (ulong?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        return Task.Run(() =>
        {
            if (pidOrder.Count == 0)
                return (IReadOnlyList<MusicMetadata>)Array.Empty<MusicMetadata>();

            var wanted = new HashSet<ulong>(pidOrder);
            var query = MPMediaQuery.SongsQuery;
            var found = (query.Items ?? [])
                .Where(i => wanted.Contains(i.PersistentID))
                .GroupBy(i => i.PersistentID)
                .ToDictionary(g => g.Key, g => ToMusicMetadata(g.First()));

            var ordered = new List<MusicMetadata>();
            foreach (var pid in pidOrder)
            {
                if (found.TryGetValue(pid, out var track))
                    ordered.Add(track);
            }

            return (IReadOnlyList<MusicMetadata>)ordered.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<string>>> GetGenresAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var items = GetFilteredItems(filter);
            return (IReadOnlyList<GroupedCount<string>>)items
                .Where(i => !string.IsNullOrWhiteSpace(i.Genre))
                .GroupBy(i => i.Genre!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GroupedCount<string>(g.Key, g.Count()))
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<int>>> GetYearsAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var items = GetFilteredItems(filter);
            return (IReadOnlyList<GroupedCount<int>>)items
                .Select(GetReleaseYear)
                .Where(y => y > 0)
                .GroupBy(y => y)
                .OrderBy(g => g.Key)
                .Select(g => new GroupedCount<int>(g.Key, g.Count()))
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<int>>> GetDecadesAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var items = GetFilteredItems(filter);
            return (IReadOnlyList<GroupedCount<int>>)items
                .Select(GetReleaseYear)
                .Where(y => y > 0)
                .Select(y => y / 10 * 10)
                .GroupBy(d => d)
                .OrderBy(g => g.Key)
                .Select(g => new GroupedCount<int>(g.Key, g.Count()))
                .ToList()
                .AsReadOnly();
        });
    }

    public async Task<IReadOnlyList<PlaylistInfo>> GetPlaylistsAsync()
    {
        var playlists = await Task.Run(() =>
        {
            var result = new List<PlaylistInfo>();
            var query = MPMediaQuery.PlaylistsQuery;
            var collections = query.Collections;
            if (collections != null)
            {
                foreach (var collection in collections)
                {
                    if (collection is MPMediaPlaylist playlist)
                    {
                        var name = playlist.Name;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.Add(new PlaylistInfo(
                                playlist.PersistentID.ToString(),
                                name,
                                (int)(playlist.Count)
                            ));
                        }
                    }
                }
            }
            return result;
        });

        var custom = await customPlaylists.LoadAllAsync();
        playlists.AddRange(custom.Select(c => new PlaylistInfo(c.Id, c.Name, c.Tracks.Length)));

        return playlists
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public async Task<PlaylistInfo?> GetPlaylistByIdAsync(string playlistId)
    {
        if (CustomPlaylistStore.IsCustomPlaylistId(playlistId))
        {
            var custom = await customPlaylists.GetByIdAsync(playlistId);
            return custom == null ? null : new PlaylistInfo(custom.Id, custom.Name, custom.Tracks.Length);
        }

        return await Task.Run(() =>
        {
            if (!ulong.TryParse(playlistId, out var pid))
                return (PlaylistInfo?)null;

            var query = MPMediaQuery.PlaylistsQuery;
            var playlist = query.Collections?
                .OfType<MPMediaPlaylist>()
                .FirstOrDefault(p => p.PersistentID == pid);

            var name = playlist?.Name;
            if (playlist == null || string.IsNullOrWhiteSpace(name))
                return (PlaylistInfo?)null;

            return (PlaylistInfo?)new PlaylistInfo(
                playlist.PersistentID.ToString(),
                name,
                (int)playlist.Count
            );
        });
    }

    public async Task<IReadOnlyList<MusicMetadata>> GetPlaylistTracksAsync(string playlistId)
    {
        if (CustomPlaylistStore.IsCustomPlaylistId(playlistId))
        {
            var tracks = await customPlaylists.GetTracksAsync(playlistId);
            return tracks;
        }

        return await Task.Run(() =>
        {
            if (!ulong.TryParse(playlistId, out var pid))
                return (IReadOnlyList<MusicMetadata>)Array.Empty<MusicMetadata>();

            var query = MPMediaQuery.PlaylistsQuery;
            var playlist = query.Collections?
                .OfType<MPMediaPlaylist>()
                .FirstOrDefault(p => p.PersistentID == pid);

            if (playlist?.Items == null)
                return (IReadOnlyList<MusicMetadata>)Array.Empty<MusicMetadata>();

            return (IReadOnlyList<MusicMetadata>)playlist.Items
                .Select(ToMusicMetadata)
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<string?> GetAlbumArtPathAsync(string trackId)
    {
        return Task.Run(() =>
        {
            if (!ulong.TryParse(trackId, out var pid))
                return (string?)null;

            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "albumart"
            );
            var filePath = Path.Combine(cacheDir, $"{trackId}.jpg");
            if (File.Exists(filePath))
                return filePath;

            var query = MPMediaQuery.SongsQuery;
            var item = query.Items?.FirstOrDefault(i => i.PersistentID == pid);
            var artwork = item?.Artwork;
            if (artwork == null)
                return (string?)null;

            var image = artwork.ImageWithSize(new CoreGraphics.CGSize(600, 600));
            if (image == null)
                return (string?)null;

            var data = image.AsJPEG(0.9f);
            if (data == null)
                return (string?)null;

            Directory.CreateDirectory(cacheDir);
            data.Save(filePath, true);
            return filePath;
        });
    }

    public Task<bool> CopyTrackAsync(MusicMetadata track, string destinationPath)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (!ulong.TryParse(track.Id, out var pid))
                    return false;

                var query = MPMediaQuery.SongsQuery;
                var item = query.Items?.FirstOrDefault(i => i.PersistentID == pid);
                var assetUrl = item?.AssetURL;
                if (assetUrl == null)
                    return false;

                var asset = AVAsset.FromUrl(assetUrl);
                if (!asset.Exportable)
                    return false;

                var dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var session = new AVAssetExportSession(asset, AVAssetExportSessionPreset.AppleM4A);
                session.OutputFileType = AVFileTypes.AppleM4a.GetConstant()?.ToString();
                session.OutputUrl = NSUrl.FromFilename(destinationPath);

                var tcs = new TaskCompletionSource<bool>();
                session.ExportAsynchronously(() =>
                {
                    tcs.SetResult(session.Status == AVAssetExportSessionStatus.Completed);
                });
                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        });
    }

    // The analysis reads the source through AVAssetReader converted to mono 16-bit PCM at this rate, so the
    // per-window maths is identical to the Android path and independent of the source's channels / rate.
    const int AnalyzeSampleRate = 44100;

    public Task<AudioLevels?> AnalyzeLevelsAsync(string trackId, TimeSpan? window = null)
    {
        var win = window ?? TimeSpan.FromMilliseconds(500);

        return Task.Run(() =>
        {
            try
            {
                if (!ulong.TryParse(trackId, out var pid))
                    return (AudioLevels?)null;

                var query = MPMediaQuery.SongsQuery;
                var item = query.Items?.FirstOrDefault(i => i.PersistentID == pid);
                var assetUrl = item?.AssetURL;
                if (assetUrl == null)
                    return null; // cloud-only / DRM item with no local asset URL

                // Runs on a background thread, so the synchronous track access (which blocks until the
                // asset's properties load) can never freeze the UI; the caller also applies a timeout.
                var asset = new AVUrlAsset(assetUrl);
                var track = asset.GetTracks(AVMediaTypes.Audio).FirstOrDefault();
                if (track == null)
                    return null;

                var duration = TimeSpan.FromSeconds(item!.PlaybackDuration);
                return DecodeLevels(asset, track, duration, win);
            }
            catch
            {
                return null;
            }
        });
    }

    static AudioLevels? DecodeLevels(AVAsset asset, AVAssetTrack track, TimeSpan duration, TimeSpan window)
    {
        var reader = new AVAssetReader(asset, out var error);
        if (error != null)
            return null; // DRM-protected assets fail to open here

        using (reader)
        {
            // Ask the reader to hand us mono, 16-bit, little-endian, interleaved PCM (it resamples / down-mixes).
            var settings = new AudioSettings
            {
                Format = AudioFormatType.LinearPCM,
                SampleRate = AnalyzeSampleRate,
                NumberChannels = 1,
                LinearPcmBitDepth = 16,
                LinearPcmFloat = false,
                LinearPcmBigEndian = false,
                LinearPcmNonInterleaved = false
            };

            var output = new AVAssetReaderTrackOutput(track, settings);
            if (!reader.CanAddOutput(output))
                return null;

            reader.AddOutput(output);
            if (!reader.StartReading())
                return null;

            var samplesPerWindow = Math.Max(1, (int)(window.TotalSeconds * AnalyzeSampleRate));
            var rms = new List<float>();
            var peaks = new List<float>();
            double sumSquares = 0;
            float peak = 0;
            var count = 0;

            while (true)
            {
                using var sample = output.CopyNextSampleBuffer();
                if (sample == null)
                    break;

                using var block = sample.GetDataBuffer();
                if (block == null)
                    continue;

                var length = (int)block.DataLength;
                if (length <= 0)
                    continue;

                var bytes = new byte[length];
                var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    block.CopyDataBytes(0, (uint)length, handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }

                for (var off = 0; off + 1 < bytes.Length; off += 2)
                {
                    var value = (short)(bytes[off] | (bytes[off + 1] << 8)) / 32768f;
                    sumSquares += (double)value * value;
                    var magnitude = Math.Abs(value);
                    if (magnitude > peak)
                        peak = magnitude;

                    if (++count >= samplesPerWindow)
                    {
                        rms.Add((float)Math.Sqrt(sumSquares / count));
                        peaks.Add(peak);
                        sumSquares = 0;
                        peak = 0;
                        count = 0;
                    }
                }
            }

            if (reader.Status == AVAssetReaderStatus.Failed)
                return null;

            if (count > 0)
            {
                rms.Add((float)Math.Sqrt(sumSquares / count));
                peaks.Add(peak);
            }

            if (rms.Count == 0)
                return null;

            return AudioAnalysis.Build(window, duration, rms, peaks);
        }
    }

    public async Task<bool> HasStreamingSubscriptionAsync()
    {
        try
        {
            return await DotnetShinyMusicKit.HasStreamingSubscriptionAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<MusicMetadata>> SearchCatalogAsync(string term, int limit = 25)
    {
        if (string.IsNullOrWhiteSpace(term))
            return [];

        string? json;
        try
        {
            var result = await DotnetShinyMusicKit.SearchCatalogAsync(term, limit);
            json = result?.ToString();
        }
        catch
        {
            // No authorization, no subscription, or a network/service failure — return empty rather than throw.
            return [];
        }

        if (string.IsNullOrEmpty(json))
            return [];

        var songs = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.ListCatalogSong);
        if (songs == null)
            return [];

        return songs.Select(ToCatalogMetadata).ToList();
    }

    static MusicMetadata ToCatalogMetadata(CatalogSong song) => new(
        Id: song.Id,
        Title: song.Title,
        Artist: song.Artist,
        Album: song.Album,
        Genre: song.Genre,
        Duration: TimeSpan.FromMilliseconds(song.DurationMillis),
        AlbumArtUri: song.ArtworkUrl,
        IsExplicit: song.IsExplicit,
        ContentUri: string.Empty,
        StoreId: null,
        Year: song.Year > 0 ? song.Year : null,
        PlayCount: 0,
        CatalogId: song.Id
    );

    public async Task<PlaylistInfo> CreatePlaylistAsync(string name)
    {
        var playlist = await customPlaylists.CreateAsync(name);
        return new PlaylistInfo(playlist.Id, playlist.Name, 0);
    }

    public Task RemovePlaylistAsync(string playlistId)
        => customPlaylists.RemoveAsync(playlistId);

    public Task AddTrackToPlaylistAsync(string playlistId, MusicMetadata track)
        => customPlaylists.AddTrackAsync(playlistId, track);

    public Task RemoveTrackFromPlaylistAsync(string playlistId, string trackId)
        => customPlaylists.RemoveTrackAsync(playlistId, trackId);

    IEnumerable<MPMediaItem> GetFilteredItems(MusicFilter? filter)
    {
        var query = MPMediaQuery.SongsQuery;
        IEnumerable<MPMediaItem> items = query.Items ?? [];

        if (filter == null)
            return items;

        if (!string.IsNullOrWhiteSpace(filter.Genre))
            items = items.Where(i => string.Equals((string?)i.Genre, filter.Genre, StringComparison.OrdinalIgnoreCase));

        if (filter.Year.HasValue)
        {
            items = items.Where(i => GetReleaseYear(i) == filter.Year.Value);
        }
        else if (filter.Decade.HasValue)
        {
            items = items.Where(i =>
            {
                var y = GetReleaseYear(i);
                return y >= filter.Decade.Value && y < filter.Decade.Value + 10;
            });
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            items = items.Where(i =>
                ContainsIgnoreCase(i.Title, filter.SearchQuery) ||
                ContainsIgnoreCase(i.Artist, filter.SearchQuery) ||
                ContainsIgnoreCase(i.AlbumTitle, filter.SearchQuery));
        }

        return items;
    }

    static bool ContainsIgnoreCase(string? value, string search)
        => value != null && ((string)value).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    static int GetReleaseYear(MPMediaItem item)
    {
        var date = item.ReleaseDate;
        if (date == null)
            return 0;
        return ((DateTime)date).Year;
    }

    static MusicMetadata ToMusicMetadata(MPMediaItem item)
    {
        var year = GetReleaseYear(item);
        var assetUrl = item.AssetURL?.AbsoluteString ?? string.Empty;
        return new MusicMetadata(
            Id: item.PersistentID.ToString(),
            Title: item.Title,
            Artist: item.Artist,
            Album: item.AlbumTitle,
            Genre: item.Genre,
            Duration: TimeSpan.FromSeconds(item.PlaybackDuration),
            AlbumArtUri: null,
            IsExplicit: item.IsExplicitItem,
            ContentUri: assetUrl,
            StoreId: item.PersistentID.ToString(),
            Year: year > 0 ? year : null,
            PlayCount: (int)item.PlayCount
        );
    }

    static PermissionStatus ToPermissionStatus(MPMediaLibraryAuthorizationStatus status) => status switch
    {
        MPMediaLibraryAuthorizationStatus.Authorized => PermissionStatus.Granted,
        MPMediaLibraryAuthorizationStatus.Denied => PermissionStatus.Denied,
        MPMediaLibraryAuthorizationStatus.Restricted => PermissionStatus.Restricted,
        _ => PermissionStatus.Unknown
    };
}
