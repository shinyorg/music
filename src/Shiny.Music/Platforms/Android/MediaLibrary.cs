using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Activity = Android.App.Activity;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

public class MediaLibrary : IMediaLibrary
{
    static readonly string[] AudioProjection = new[]
    {
        MediaStore.Audio.Media.InterfaceConsts.Id,
        MediaStore.Audio.Media.InterfaceConsts.Title,
        MediaStore.Audio.Media.InterfaceConsts.Artist,
        MediaStore.Audio.Media.InterfaceConsts.Album,
        MediaStore.Audio.Media.InterfaceConsts.Duration,
        MediaStore.Audio.Media.InterfaceConsts.AlbumId,
        MediaStore.Audio.Media.InterfaceConsts.Data
    };

    Activity GetActivity()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current activity available");
        return activity;
    }

    public Task<PermissionStatus> CheckPermissionAsync()
    {
        var activity = GetActivity();
        string permission = GetRequiredPermission();

        var result = ContextCompat.CheckSelfPermission(activity, permission);
        var status = result == Permission.Granted ? PermissionStatus.Granted : PermissionStatus.Denied;
        return Task.FromResult(status);
    }

    public Task<PermissionStatus> RequestPermissionAsync()
    {
        var tcs = new TaskCompletionSource<PermissionStatus>();
        var activity = GetActivity();
        string permission = GetRequiredPermission();

        if (ContextCompat.CheckSelfPermission(activity, permission) == Permission.Granted)
        {
            tcs.SetResult(PermissionStatus.Granted);
            return tcs.Task;
        }

        // Use MAUI Permissions API for cleaner request flow
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.StorageRead>();
                tcs.SetResult(status == Microsoft.Maui.ApplicationModel.PermissionStatus.Granted
                    ? PermissionStatus.Granted
                    : PermissionStatus.Denied);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task<IReadOnlyList<MusicMetadata>> GetAllTracksAsync()
    {
        return Task.Run(() =>
        {
            var activity = GetActivity();
            var tracks = new List<MusicMetadata>();
            var contentUri = MediaStore.Audio.Media.ExternalContentUri!;

            using var cursor = activity.ContentResolver!.Query(
                contentUri,
                AudioProjection,
                MediaStore.Audio.Media.InterfaceConsts.IsMusic + " != 0",
                null,
                MediaStore.Audio.Media.InterfaceConsts.Title + " ASC"
            );

            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    tracks.Add(ReadTrack(cursor));
                }
            }

            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<MusicMetadata>> SearchTracksAsync(string query)
    {
        return Task.Run(() =>
        {
            var activity = GetActivity();
            var tracks = new List<MusicMetadata>();
            var contentUri = MediaStore.Audio.Media.ExternalContentUri!;

            var selection = $"{MediaStore.Audio.Media.InterfaceConsts.IsMusic} != 0 AND (" +
                $"{MediaStore.Audio.Media.InterfaceConsts.Title} LIKE ? OR " +
                $"{MediaStore.Audio.Media.InterfaceConsts.Artist} LIKE ? OR " +
                $"{MediaStore.Audio.Media.InterfaceConsts.Album} LIKE ?)";
            var selectionArgs = new[] { $"%{query}%", $"%{query}%", $"%{query}%" };

            using var cursor = activity.ContentResolver!.Query(
                contentUri,
                AudioProjection,
                selection,
                selectionArgs,
                MediaStore.Audio.Media.InterfaceConsts.Title + " ASC"
            );

            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    tracks.Add(ReadTrack(cursor));
                }
            }

            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<bool> CopyTrackAsync(MusicMetadata track, string destinationPath)
    {
        return Task.Run(() =>
        {
            try
            {
                var activity = GetActivity();
                var sourceUri = Uri.Parse(track.ContentUri)!;

                using var inputStream = activity.ContentResolver!.OpenInputStream(sourceUri);
                if (inputStream == null)
                    return false;

                var dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using var outputStream = File.Create(destinationPath);
                inputStream.CopyTo(outputStream);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    static string GetRequiredPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            return Android.Manifest.Permission.ReadMediaAudio;

        return Android.Manifest.Permission.ReadExternalStorage;
    }

    static MusicMetadata ReadTrack(ICursor cursor)
    {
        var id = cursor.GetLong(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Id));
        var title = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Title));
        var artist = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Artist));
        var album = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Album));
        var durationMs = cursor.GetLong(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Duration));
        var albumId = cursor.GetLong(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.AlbumId));

        var contentUri = ContentUris.WithAppendedId(MediaStore.Audio.Media.ExternalContentUri!, id);
        var albumArtUri = ContentUris.WithAppendedId(
            Uri.Parse("content://media/external/audio/albumart")!, albumId
        );

        return new MusicMetadata(
            Id: id.ToString(),
            Title: title,
            Artist: artist,
            Album: album,
            Genre: null,
            Duration: TimeSpan.FromMilliseconds(durationMs),
            AlbumArtUri: albumArtUri?.ToString(),
            IsExplicit: null,
            ContentUri: contentUri?.ToString() ?? string.Empty
        );
    }

    public Task<IReadOnlyList<string>> GetGenresAsync()
    {
        return Task.Run(() =>
        {
            var activity = GetActivity();
            var genres = new List<string>();

            using var cursor = activity.ContentResolver!.Query(
                MediaStore.Audio.Genres.ExternalContentUri!,
                new[] { MediaStore.Audio.Genres.InterfaceConsts.Name },
                null,
                null,
                MediaStore.Audio.Genres.InterfaceConsts.Name + " ASC"
            );

            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    var name = cursor.GetString(0);
                    if (!string.IsNullOrWhiteSpace(name))
                        genres.Add(name);
                }
            }

            return (IReadOnlyList<string>)genres
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<bool> HasStreamingSubscriptionAsync() => Task.FromResult(false);
}
