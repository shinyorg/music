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
        MediaStore.Audio.Media.InterfaceConsts.Data,
        MediaStore.Audio.Media.InterfaceConsts.Year
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
        var year = cursor.GetInt(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Year));

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
            ContentUri: contentUri?.ToString() ?? string.Empty,
            Year: year > 0 ? year : null
        );
    }

    static (string Selection, string[]? Args) BuildAudioSelection(MusicFilter? filter)
    {
        var conditions = new List<string> { MediaStore.Audio.Media.InterfaceConsts.IsMusic + " != 0" };
        var args = new List<string>();

        if (filter != null)
        {
            if (filter.Year.HasValue)
            {
                conditions.Add(MediaStore.Audio.Media.InterfaceConsts.Year + " = ?");
                args.Add(filter.Year.Value.ToString());
            }
            else if (filter.Decade.HasValue)
            {
                conditions.Add(MediaStore.Audio.Media.InterfaceConsts.Year + " >= ?");
                args.Add(filter.Decade.Value.ToString());
                conditions.Add(MediaStore.Audio.Media.InterfaceConsts.Year + " < ?");
                args.Add((filter.Decade.Value + 10).ToString());
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
            {
                conditions.Add("(" +
                    MediaStore.Audio.Media.InterfaceConsts.Title + " LIKE ? OR " +
                    MediaStore.Audio.Media.InterfaceConsts.Artist + " LIKE ? OR " +
                    MediaStore.Audio.Media.InterfaceConsts.Album + " LIKE ?)");
                var searchArg = $"%{filter.SearchQuery}%";
                args.AddRange(new[] { searchArg, searchArg, searchArg });
            }
        }

        return (string.Join(" AND ", conditions), args.Count > 0 ? args.ToArray() : null);
    }

    List<(long Id, string Name)> GetAllGenreEntries(Activity activity)
    {
        var entries = new List<(long Id, string Name)>();
        using var cursor = activity.ContentResolver!.Query(
            MediaStore.Audio.Genres.ExternalContentUri!,
            new[]
            {
                MediaStore.Audio.Genres.InterfaceConsts.Id,
                MediaStore.Audio.Genres.InterfaceConsts.Name
            },
            null, null, null
        );
        if (cursor != null)
        {
            while (cursor.MoveToNext())
            {
                var id = cursor.GetLong(0);
                var name = cursor.GetString(1);
                if (!string.IsNullOrWhiteSpace(name))
                    entries.Add((id, name));
            }
        }
        return entries;
    }

    public Task<IReadOnlyList<MusicMetadata>> GetTracksAsync(MusicFilter filter)
    {
        return Task.Run(() =>
        {
            var activity = GetActivity();
            var (selection, selectionArgs) = BuildAudioSelection(filter);
            var tracks = new List<MusicMetadata>();

            if (!string.IsNullOrWhiteSpace(filter.Genre))
            {
                var genreEntries = GetAllGenreEntries(activity);
                foreach (var (id, _) in genreEntries.Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase)))
                {
                    var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                    using var cursor = activity.ContentResolver!.Query(
                        membersUri!,
                        AudioProjection,
                        selection,
                        selectionArgs,
                        MediaStore.Audio.Media.InterfaceConsts.Title + " ASC"
                    );
                    if (cursor != null)
                    {
                        while (cursor.MoveToNext())
                            tracks.Add(ReadTrack(cursor));
                    }
                }
                tracks = tracks.DistinctBy(t => t.Id).ToList();
            }
            else
            {
                using var cursor = activity.ContentResolver!.Query(
                    MediaStore.Audio.Media.ExternalContentUri!,
                    AudioProjection,
                    selection,
                    selectionArgs,
                    MediaStore.Audio.Media.InterfaceConsts.Title + " ASC"
                );
                if (cursor != null)
                {
                    while (cursor.MoveToNext())
                        tracks.Add(ReadTrack(cursor));
                }
            }

            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<string>>> GetGenresAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var activity = GetActivity();
            var genreEntries = GetAllGenreEntries(activity);
            var (selection, selectionArgs) = BuildAudioSelection(filter);

            if (!string.IsNullOrWhiteSpace(filter?.Genre))
                genreEntries = genreEntries
                    .Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var grouped = new Dictionary<string, (string Name, int Count)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, name) in genreEntries)
            {
                var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                using var membersCursor = activity.ContentResolver!.Query(
                    membersUri!,
                    new[] { MediaStore.Audio.Media.InterfaceConsts.Id },
                    selection,
                    selectionArgs,
                    null
                );
                var count = membersCursor?.Count ?? 0;
                if (count > 0)
                {
                    if (grouped.TryGetValue(name, out var existing))
                        grouped[name] = (existing.Name, existing.Count + count);
                    else
                        grouped[name] = (name, count);
                }
            }

            return (IReadOnlyList<GroupedCount<string>>)grouped.Values
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new GroupedCount<string>(g.Name, g.Count))
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<IReadOnlyList<GroupedCount<int>>> GetYearsAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var activity = GetActivity();
            var (selection, selectionArgs) = BuildAudioSelection(filter);
            selection += " AND " + MediaStore.Audio.Media.InterfaceConsts.Year + " > 0";

            var years = new List<int>();
            var projection = new[] { MediaStore.Audio.Media.InterfaceConsts.Year };

            if (!string.IsNullOrWhiteSpace(filter?.Genre))
            {
                var genreEntries = GetAllGenreEntries(activity);
                foreach (var (id, _) in genreEntries.Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase)))
                {
                    var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                    using var cursor = activity.ContentResolver!.Query(membersUri!, projection, selection, selectionArgs, null);
                    if (cursor != null)
                    {
                        while (cursor.MoveToNext())
                        {
                            var year = cursor.GetInt(0);
                            if (year > 0) years.Add(year);
                        }
                    }
                }
            }
            else
            {
                using var cursor = activity.ContentResolver!.Query(
                    MediaStore.Audio.Media.ExternalContentUri!, projection, selection, selectionArgs, null);
                if (cursor != null)
                {
                    while (cursor.MoveToNext())
                    {
                        var year = cursor.GetInt(0);
                        if (year > 0) years.Add(year);
                    }
                }
            }

            return (IReadOnlyList<GroupedCount<int>>)years
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
            var activity = GetActivity();
            var (selection, selectionArgs) = BuildAudioSelection(filter);
            selection += " AND " + MediaStore.Audio.Media.InterfaceConsts.Year + " > 0";

            var decades = new List<int>();
            var projection = new[] { MediaStore.Audio.Media.InterfaceConsts.Year };

            if (!string.IsNullOrWhiteSpace(filter?.Genre))
            {
                var genreEntries = GetAllGenreEntries(activity);
                foreach (var (id, _) in genreEntries.Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase)))
                {
                    var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                    using var cursor = activity.ContentResolver!.Query(membersUri!, projection, selection, selectionArgs, null);
                    if (cursor != null)
                    {
                        while (cursor.MoveToNext())
                        {
                            var year = cursor.GetInt(0);
                            if (year > 0) decades.Add(year / 10 * 10);
                        }
                    }
                }
            }
            else
            {
                using var cursor = activity.ContentResolver!.Query(
                    MediaStore.Audio.Media.ExternalContentUri!, projection, selection, selectionArgs, null);
                if (cursor != null)
                {
                    while (cursor.MoveToNext())
                    {
                        var year = cursor.GetInt(0);
                        if (year > 0) decades.Add(year / 10 * 10);
                    }
                }
            }

            return (IReadOnlyList<GroupedCount<int>>)decades
                .GroupBy(d => d)
                .OrderBy(g => g.Key)
                .Select(g => new GroupedCount<int>(g.Key, g.Count()))
                .ToList()
                .AsReadOnly();
        });
    }

    public Task<bool> HasStreamingSubscriptionAsync() => Task.FromResult(false);
}
