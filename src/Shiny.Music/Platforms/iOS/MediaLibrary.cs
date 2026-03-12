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
            StoreId: item.PlaybackStoreID
        );
    }

    public Task<IReadOnlyList<string>> GetGenresAsync()
    {
        return Task.Run(() =>
        {
            var query = MPMediaQuery.GenresQuery;
            var collections = query.Collections ?? Array.Empty<MPMediaItemCollection>();

            var genres = collections
                .Select(c => (string?)c.RepresentativeItem?.Genre)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList()!;

            return (IReadOnlyList<string>)genres.AsReadOnly();
        });
    }

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
