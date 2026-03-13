using AVFoundation;
using Foundation;
using MediaPlayer;
using StoreKit;

namespace Shiny.Music;

public class MediaLibrary : IMediaLibrary
{
    public Task<PermissionStatus> CheckPermissionAsync()
    {
        var status = MPMediaLibrary.AuthorizationStatus;
        return Task.FromResult(ToPermissionStatus(status));
    }

    public Task<PermissionStatus> RequestPermissionAsync()
    {
        var tcs = new TaskCompletionSource<PermissionStatus>();

        var current = MPMediaLibrary.AuthorizationStatus;
        if (current == MPMediaLibraryAuthorizationStatus.Authorized)
        {
            tcs.SetResult(PermissionStatus.Granted);
            return tcs.Task;
        }

        if (current == MPMediaLibraryAuthorizationStatus.Restricted)
        {
            tcs.SetResult(PermissionStatus.Restricted);
            return tcs.Task;
        }

        if (current == MPMediaLibraryAuthorizationStatus.Denied)
        {
            tcs.SetResult(PermissionStatus.Denied);
            return tcs.Task;
        }

        MPMediaLibrary.RequestAuthorization(status =>
        {
            tcs.SetResult(ToPermissionStatus(status));
        });

        return tcs.Task;
    }

    public Task<IReadOnlyList<MusicMetadata>> GetAllTracksAsync()
    {
        return Task.Run(() =>
        {
            var query = new MPMediaQuery();
            query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
                NSNumber.FromInt32((int)MPMediaType.Music),
                MPMediaItem.MediaTypeProperty,
                MPMediaPredicateComparison.EqualsTo
            ));

            var items = query.Items ?? Array.Empty<MPMediaItem>();
            var tracks = items.Select(ToMusicMetadata).ToList();
            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<MusicMetadata>> SearchTracksAsync(string searchQuery)
    {
        return Task.Run(() =>
        {
            var query = new MPMediaQuery();
            query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
                NSNumber.FromInt32((int)MPMediaType.Music),
                MPMediaItem.MediaTypeProperty,
                MPMediaPredicateComparison.EqualsTo
            ));

            var items = query.Items ?? Array.Empty<MPMediaItem>();
            var lowerQuery = searchQuery.ToLower();

            var tracks = items
                .Where(item =>
                    ((string?)item.Title)?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) == true ||
                    ((string?)item.Artist)?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) == true ||
                    ((string?)item.AlbumTitle)?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) == true)
                .Select(ToMusicMetadata)
                .ToList();

            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<bool> CopyTrackAsync(MusicMetadata track, string destinationPath)
    {
        return Task.Run(async () =>
        {
            try
            {
                // On iOS, only non-DRM tracks expose an AssetURL that can be exported.
                // DRM-protected (Apple Music subscription) tracks cannot be copied.
                var assetUrl = NSUrl.FromString(track.ContentUri);
                if (assetUrl == null)
                    return false;

                var asset = AVAsset.FromUrl(assetUrl);
                if (!asset.Exportable)
                    return false;

                var exportSession = new AVAssetExportSession(asset, AVAssetExportSessionPreset.AppleM4A);
                exportSession.OutputFileType = AVFileTypes.AppleM4a.GetConstant()?.ToString();

                var destUrl = NSUrl.FromFilename(destinationPath);
                exportSession.OutputUrl = destUrl;

                var tcs = new TaskCompletionSource<bool>();
                exportSession.ExportAsynchronously(() =>
                {
                    tcs.SetResult(exportSession.Status == AVAssetExportSessionStatus.Completed);
                });

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        });
    }

    static PermissionStatus ToPermissionStatus(MPMediaLibraryAuthorizationStatus status) => status switch
    {
        MPMediaLibraryAuthorizationStatus.Authorized => PermissionStatus.Granted,
        MPMediaLibraryAuthorizationStatus.Denied => PermissionStatus.Denied,
        MPMediaLibraryAuthorizationStatus.Restricted => PermissionStatus.Restricted,
        _ => PermissionStatus.Unknown
    };

    static int? GetReleaseYear(MPMediaItem item)
    {
        var date = item.ReleaseDate;
        if (date == null)
            return null;
        var year = ((DateTime)date).Year;
        return year > 0 ? year : null;
    }

    static MusicMetadata ToMusicMetadata(MPMediaItem item)
    {
        return new MusicMetadata(
            Id: item.PersistentID.ToString(),
            Title: item.Title,
            Artist: item.Artist,
            Album: item.AlbumTitle,
            Genre: item.Genre,
            Duration: TimeSpan.FromSeconds(item.PlaybackDuration),
            AlbumArtUri: null, // iOS album art is accessed via MPMediaItem.Artwork, not a URI
            IsExplicit: item.IsExplicitItem,
            ContentUri: item.AssetURL?.AbsoluteString ?? string.Empty,
            StoreId: item.PlaybackStoreID,
            Year: GetReleaseYear(item)
        );
    }

    static IEnumerable<MPMediaItem> GetFilteredItems(MusicFilter? filter)
    {
        var query = new MPMediaQuery();
        query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
            NSNumber.FromInt32((int)MPMediaType.Music),
            MPMediaItem.MediaTypeProperty,
            MPMediaPredicateComparison.EqualsTo
        ));

        if (!string.IsNullOrWhiteSpace(filter?.Genre))
        {
            query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
                new NSString(filter.Genre),
                MPMediaItem.GenreProperty,
                MPMediaPredicateComparison.EqualsTo
            ));
        }

        IEnumerable<MPMediaItem> items = query.Items ?? Array.Empty<MPMediaItem>();

        if (filter != null)
        {
            if (filter.Year.HasValue)
            {
                items = items.Where(item => GetReleaseYear(item) == filter.Year.Value);
            }
            else if (filter.Decade.HasValue)
            {
                items = items.Where(item =>
                {
                    var year = GetReleaseYear(item);
                    return year.HasValue && year.Value >= filter.Decade.Value && year.Value < filter.Decade.Value + 10;
                });
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                items = items.Where(item =>
                    ((string?)item.Title)?.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase) == true ||
                    ((string?)item.Artist)?.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase) == true ||
                    ((string?)item.AlbumTitle)?.Contains(filter.SearchQuery, StringComparison.OrdinalIgnoreCase) == true);
            }
        }

        return items;
    }

    public Task<IReadOnlyList<MusicMetadata>> GetTracksAsync(MusicFilter filter)
    {
        return Task.Run(() =>
        {
            var tracks = GetFilteredItems(filter).Select(ToMusicMetadata).ToList();
            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<string>>> GetGenresAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var genres = GetFilteredItems(filter)
                .Select(item => (string?)item.Genre)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .GroupBy(g => g!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GroupedCount<string>(g.Key, g.Count()))
                .ToList();

            return (IReadOnlyList<GroupedCount<string>>)genres.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<int>>> GetYearsAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var years = GetFilteredItems(filter)
                .Select(GetReleaseYear)
                .Where(y => y.HasValue && y.Value > 0)
                .Select(y => y!.Value)
                .GroupBy(y => y)
                .OrderBy(g => g.Key)
                .Select(g => new GroupedCount<int>(g.Key, g.Count()))
                .ToList();

            return (IReadOnlyList<GroupedCount<int>>)years.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<int>>> GetDecadesAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var decades = GetFilteredItems(filter)
                .Select(GetReleaseYear)
                .Where(y => y.HasValue && y.Value > 0)
                .Select(y => y!.Value / 10 * 10)
                .GroupBy(d => d)
                .OrderBy(g => g.Key)
                .Select(g => new GroupedCount<int>(g.Key, g.Count()))
                .ToList();

            return (IReadOnlyList<GroupedCount<int>>)decades.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<PlaylistInfo>> GetPlaylistsAsync()
    {
        return Task.Run(() =>
        {
            var query = MPMediaQuery.PlaylistsQuery;
            var collections = query.Collections ?? Array.Empty<MPMediaItemCollection>();

            var playlists = collections
                .OfType<MPMediaPlaylist>()
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(p => new PlaylistInfo(
                    Id: p.PersistentID.ToString(),
                    Name: p.Name!,
                    SongCount: (int)(p.Items?.Length ?? 0)
                ))
                .ToList();

            return (IReadOnlyList<PlaylistInfo>)playlists.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<MusicMetadata>> GetPlaylistTracksAsync(string playlistId)
    {
        return Task.Run(() =>
        {
            if (!ulong.TryParse(playlistId, out var persistentId))
                return (IReadOnlyList<MusicMetadata>)Array.Empty<MusicMetadata>();

            var query = MPMediaQuery.PlaylistsQuery;
            query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
                NSNumber.FromUInt64(persistentId),
                MPMediaPlaylist.PersistentIDProperty,
                MPMediaPredicateComparison.EqualsTo
            ));

            var collections = query.Collections ?? Array.Empty<MPMediaItemCollection>();
            var playlist = collections.OfType<MPMediaPlaylist>().FirstOrDefault();

            if (playlist == null)
                return (IReadOnlyList<MusicMetadata>)Array.Empty<MusicMetadata>();

            var tracks = (playlist.Items ?? Array.Empty<MPMediaItem>())
                .Select(ToMusicMetadata)
                .ToList();

            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<PlaylistInfo?> CreatePlaylistAsync(string name)
    {
        var tcs = new TaskCompletionSource<PlaylistInfo?>();

        var metadata = new MPMediaPlaylistCreationMetadata(name);
        MPMediaLibrary.GetDefaultMediaLibrary().AddPlaylist(metadata, (playlist, error) =>
        {
            if (error != null || playlist == null)
                tcs.SetResult(null);
            else
                tcs.SetResult(new PlaylistInfo(playlist.PersistentID.ToString(), playlist.Name!, 0));
        });

        return tcs.Task;
    }

    // iOS MediaPlayer framework does not expose a public API to rename playlists.
    public Task<bool> RenamePlaylistAsync(string playlistId, string newName) => Task.FromResult(false);

    // iOS MediaPlayer framework does not expose a public API to delete playlists.
    public Task<bool> DeletePlaylistAsync(string playlistId) => Task.FromResult(false);

    public async Task<bool> AddTracksToPlaylistAsync(string playlistId, IEnumerable<MusicMetadata> tracks)
    {
        if (!ulong.TryParse(playlistId, out var persistentId))
            return false;

        var query = MPMediaQuery.PlaylistsQuery;
        query.AddFilterPredicate(MPMediaPropertyPredicate.PredicateWithValue(
            NSNumber.FromUInt64(persistentId),
            MPMediaPlaylist.PersistentIDProperty,
            MPMediaPredicateComparison.EqualsTo
        ));

        var collections = query.Collections ?? Array.Empty<MPMediaItemCollection>();
        var playlist = collections.OfType<MPMediaPlaylist>().FirstOrDefault();
        if (playlist == null)
            return false;

        var addedAny = false;
        foreach (var track in tracks)
        {
            // iOS requires an Apple Music catalog product ID (StoreId); local-only tracks cannot be added.
            if (string.IsNullOrEmpty(track.StoreId))
                continue;

            var tcs = new TaskCompletionSource<bool>();
            playlist.AddItem(track.StoreId, error => tcs.SetResult(error == null));
            if (await tcs.Task)
                addedAny = true;
        }

        return addedAny;
    }

    // iOS MediaPlayer framework does not expose a public API to remove individual tracks from playlists.
    public Task<bool> RemoveTrackFromPlaylistAsync(string playlistId, string trackId) => Task.FromResult(false);

    public async Task<bool> HasStreamingSubscriptionAsync()
    {
        try
        {
#pragma warning disable CA1422 // SKCloudServiceController is obsoleted on iOS 18+ but no .NET replacement is available
            var controller = new SKCloudServiceController();
            var capabilities = await controller.RequestCapabilitiesAsync();
            return capabilities.HasFlag(SKCloudServiceCapability.MusicCatalogPlayback);
#pragma warning restore CA1422
        }
        catch
        {
            return false;
        }
    }
}
