using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.Provider;
using AndroidX.Core.Content;
using Activity = Android.App.Activity;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

public class MediaLibrary(ActivityProvider activityProvider, PlayCountStore playCounts) : IMediaLibrary
{
    readonly CustomPlaylistStore customPlaylists = new();
    static readonly string[] AudioProjection = 
    [
        MediaStore.Audio.Media.InterfaceConsts.Id,
        MediaStore.Audio.Media.InterfaceConsts.Title,
        MediaStore.Audio.Media.InterfaceConsts.Artist,
        MediaStore.Audio.Media.InterfaceConsts.Album,
        MediaStore.Audio.Media.InterfaceConsts.Duration,
        MediaStore.Audio.Media.InterfaceConsts.AlbumId,
        MediaStore.Audio.Media.InterfaceConsts.Data,
        MediaStore.Audio.Media.InterfaceConsts.Year
    ];

    ContentResolver Resolver => Application.Context.ContentResolver!;

    public Task<PermissionStatus> CheckPermissionAsync()
    {
        string permission = GetRequiredPermission();

        var result = ContextCompat.CheckSelfPermission(Application.Context, permission);
        var status = result == Permission.Granted ? PermissionStatus.Granted : PermissionStatus.Denied;
        return Task.FromResult(status);
    }

    public Task<PermissionStatus> RequestPermissionAsync()
    {
        string permission = GetRequiredPermission();

        if (ContextCompat.CheckSelfPermission(Application.Context, permission) == Permission.Granted)
            return Task.FromResult(PermissionStatus.Granted);

        var activity = activityProvider.Current;
        if (activity is not AndroidX.Fragment.App.FragmentActivity fragmentActivity)
            throw new InvalidOperationException("Current activity must be a FragmentActivity to request permissions");

        var fragment = new PermissionRequestFragment();
        return fragment.RequestAsync(fragmentActivity, permission);
    }

    public async Task<IReadOnlyList<MusicMetadata>> GetAllTracksAsync()
    {
        var tracks = await Task.Run(() =>
        {
            var genreMap = BuildGenreMap();
            var result = new List<MusicMetadata>();
            var contentUri = MediaStore.Audio.Media.ExternalContentUri!;

            using var cursor = Resolver.Query(
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
                    result.Add(ReadTrack(cursor, genreMap));
                }
            }

            return result;
        });

        return await WithPlayCounts(tracks);
    }

    public async Task<IReadOnlyList<MusicMetadata>> SearchTracksAsync(string query)
    {
        var tracks = await Task.Run(() =>
        {
            var genreMap = BuildGenreMap();
            var result = new List<MusicMetadata>();
            var contentUri = MediaStore.Audio.Media.ExternalContentUri!;

            var selection = $"{MediaStore.Audio.Media.InterfaceConsts.IsMusic} != 0 AND (" +
                $"{MediaStore.Audio.Media.InterfaceConsts.Title} LIKE ? OR " +
                $"{MediaStore.Audio.Media.InterfaceConsts.Artist} LIKE ? OR " +
                $"{MediaStore.Audio.Media.InterfaceConsts.Album} LIKE ?)";
            var selectionArgs = new[] { $"%{query}%", $"%{query}%", $"%{query}%" };

            using var cursor = Resolver.Query(
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
                    result.Add(ReadTrack(cursor, genreMap));
                }
            }

            return result;
        });

        return await WithPlayCounts(tracks);
    }

    public Task<bool> CopyTrackAsync(MusicMetadata track, string destinationPath)
    {
        return Task.Run(() =>
        {
            try
            {
                var sourceUri = Uri.Parse(track.ContentUri)!;

                using var inputStream = Resolver.OpenInputStream(sourceUri);
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

    async Task<IReadOnlyList<MusicMetadata>> WithPlayCounts(List<MusicMetadata> tracks)
    {
        var counts = await playCounts.LoadAllAsync();
        if (counts.Count == 0)
            return tracks.AsReadOnly();

        return tracks
            .Select(t => counts.TryGetValue(t.Id, out var count) ? t with { PlayCount = count } : t)
            .ToList()
            .AsReadOnly();
    }

    static string GetRequiredPermission()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            return Android.Manifest.Permission.ReadMediaAudio;

        return Android.Manifest.Permission.ReadExternalStorage;
    }

    static MusicMetadata ReadTrack(ICursor cursor, Dictionary<long, string>? genreMap = null)
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

        string? genre = null;
        genreMap?.TryGetValue(id, out genre);

        return new MusicMetadata(
            Id: id.ToString(),
            Title: title,
            Artist: artist,
            Album: album,
            Genre: genre,
            Duration: TimeSpan.FromMilliseconds(durationMs),
            AlbumArtUri: albumArtUri?.ToString(),
            IsExplicit: null,
            ContentUri: contentUri?.ToString() ?? string.Empty,
            Year: year > 0 ? year : null
        );
    }

    Dictionary<long, string> BuildGenreMap()
    {
        var map = new Dictionary<long, string>();
        var genreEntries = GetAllGenreEntries();
        foreach (var (genreId, genreName) in genreEntries)
        {
            var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", genreId);
            using var cursor = Resolver.Query(
                membersUri!,
                new[] { MediaStore.Audio.Media.InterfaceConsts.Id },
                null, null, null
            );
            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    var trackId = cursor.GetLong(0);
                    map.TryAdd(trackId, genreName);
                }
            }
        }
        return map;
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
                args.AddRange([ searchArg, searchArg, searchArg ]);
            }
        }

        return (string.Join(" AND ", conditions), args.Count > 0 ? args.ToArray() : null);
    }

    List<(long Id, string Name)> GetAllGenreEntries()
    {
        var entries = new List<(long Id, string Name)>();
        using var cursor = Resolver.Query(
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

    public async Task<IReadOnlyList<MusicMetadata>> GetTracksAsync(MusicFilter filter)
    {
        var tracks = await Task.Run(() =>
        {
            var genreMap = BuildGenreMap();
            var (selection, selectionArgs) = BuildAudioSelection(filter);
            var result = new List<MusicMetadata>();

            if (!string.IsNullOrWhiteSpace(filter.Genre))
            {
                var genreEntries = GetAllGenreEntries();
                foreach (var (id, _) in genreEntries.Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase)))
                {
                    var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                    using var cursor = Resolver.Query(
                        membersUri!,
                        AudioProjection,
                        selection,
                        selectionArgs,
                        MediaStore.Audio.Media.InterfaceConsts.Title + " ASC"
                    );
                    if (cursor != null)
                    {
                        while (cursor.MoveToNext())
                            result.Add(ReadTrack(cursor, genreMap));
                    }
                }
                result = result.DistinctBy(t => t.Id).ToList();
            }
            else
            {
                using var cursor = Resolver.Query(
                    MediaStore.Audio.Media.ExternalContentUri!,
                    AudioProjection,
                    selection,
                    selectionArgs,
                    MediaStore.Audio.Media.InterfaceConsts.Title + " ASC"
                );
                if (cursor != null)
                {
                    while (cursor.MoveToNext())
                        result.Add(ReadTrack(cursor, genreMap));
                }
            }

            return result;
        });

        return await WithPlayCounts(tracks);
    }

    public Task<IReadOnlyList<GroupedCount<string>>> GetGenresAsync(MusicFilter? filter = null)
    {
        return Task.Run(() =>
        {
            var genreEntries = GetAllGenreEntries();
            var (selection, selectionArgs) = BuildAudioSelection(filter);

            if (!string.IsNullOrWhiteSpace(filter?.Genre))
                genreEntries = genreEntries
                    .Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var grouped = new Dictionary<string, (string Name, int Count)>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, name) in genreEntries)
            {
                var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                using var membersCursor = Resolver.Query(
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
            var (selection, selectionArgs) = BuildAudioSelection(filter);
            selection += " AND " + MediaStore.Audio.Media.InterfaceConsts.Year + " > 0";

            var years = new List<int>();
            var projection = new[] { MediaStore.Audio.Media.InterfaceConsts.Year };

            if (!string.IsNullOrWhiteSpace(filter?.Genre))
            {
                var genreEntries = GetAllGenreEntries();
                foreach (var (id, _) in genreEntries.Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase)))
                {
                    var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                    using var cursor = Resolver.Query(membersUri!, projection, selection, selectionArgs, null);
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
                using var cursor = Resolver.Query(
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
            var (selection, selectionArgs) = BuildAudioSelection(filter);
            selection += " AND " + MediaStore.Audio.Media.InterfaceConsts.Year + " > 0";

            var decades = new List<int>();
            var projection = new[] { MediaStore.Audio.Media.InterfaceConsts.Year };

            if (!string.IsNullOrWhiteSpace(filter?.Genre))
            {
                var genreEntries = GetAllGenreEntries();
                foreach (var (id, _) in genreEntries.Where(e => string.Equals(e.Name, filter.Genre, StringComparison.OrdinalIgnoreCase)))
                {
                    var membersUri = MediaStore.Audio.Genres.Members.GetContentUri("external", id);
                    using var cursor = Resolver.Query(membersUri!, projection, selection, selectionArgs, null);
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
                using var cursor = Resolver.Query(
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

    public async Task<IReadOnlyList<PlaylistInfo>> GetPlaylistsAsync()
    {
        var playlists = await Task.Run(() =>
        {
            var result = new List<PlaylistInfo>();

            using var cursor = Resolver.Query(
                MediaStore.Audio.Playlists.ExternalContentUri!,
                new[]
                {
                    MediaStore.Audio.Playlists.InterfaceConsts.Id,
                    MediaStore.Audio.Playlists.InterfaceConsts.Name
                },
                null, null,
                MediaStore.Audio.Playlists.InterfaceConsts.Name + " ASC"
            );

            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    var id = cursor.GetLong(0);
                    var name = cursor.GetString(1);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var membersUri = MediaStore.Audio.Playlists.Members.GetContentUri("external", id)!;
                    using var membersCursor = Resolver.Query(
                        membersUri,
                        [ MediaStore.Audio.Playlists.Members.AudioId ],
                        null, null, null
                    );
                    var count = membersCursor?.Count ?? 0;
                    result.Add(new PlaylistInfo(id.ToString(), name, count));
                }
            }

            return result;
        });

        var custom = await this.customPlaylists.LoadAllAsync();
        playlists.AddRange(custom.Select(c => new PlaylistInfo(c.Id, c.Name, c.Tracks.Length)));

        return playlists
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<MusicMetadata>> GetPlaylistTracksAsync(string playlistId)
    {
        if (CustomPlaylistStore.IsCustomPlaylistId(playlistId))
        {
            var tracks = await this.customPlaylists.GetTracksAsync(playlistId);
            return tracks;
        }

        return await Task.Run(() =>
        {
            var genreMap = BuildGenreMap();
            var tracks = new List<MusicMetadata>();

            if (!long.TryParse(playlistId, out var id))
                return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();

            var membersUri = MediaStore.Audio.Playlists.Members.GetContentUri("external", id)!;

            var projection = new[]
            {
                MediaStore.Audio.Playlists.Members.AudioId,
                MediaStore.Audio.Media.InterfaceConsts.Title,
                MediaStore.Audio.Media.InterfaceConsts.Artist,
                MediaStore.Audio.Media.InterfaceConsts.Album,
                MediaStore.Audio.Media.InterfaceConsts.Duration,
                MediaStore.Audio.Media.InterfaceConsts.AlbumId,
                MediaStore.Audio.Media.InterfaceConsts.Data,
                MediaStore.Audio.Media.InterfaceConsts.Year,
                MediaStore.Audio.Playlists.Members.PlayOrder
            };

            using var cursor = Resolver.Query(
                membersUri,
                projection,
                MediaStore.Audio.Media.InterfaceConsts.IsMusic + " != 0",
                null,
                MediaStore.Audio.Playlists.Members.PlayOrder + " ASC"
            );

            if (cursor != null)
            {
                while (cursor.MoveToNext())
                {
                    var trackId = cursor.GetLong(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Playlists.Members.AudioId));
                    var title = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Title));
                    var artist = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Artist));
                    var album = cursor.GetString(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Album));
                    var durationMs = cursor.GetLong(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Duration));
                    var albumId = cursor.GetLong(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.AlbumId));
                    var year = cursor.GetInt(cursor.GetColumnIndexOrThrow(MediaStore.Audio.Media.InterfaceConsts.Year));

                    var contentUri = ContentUris.WithAppendedId(MediaStore.Audio.Media.ExternalContentUri!, trackId);
                    var albumArtUri = ContentUris.WithAppendedId(
                        Uri.Parse("content://media/external/audio/albumart")!, albumId
                    );

                    genreMap.TryGetValue(trackId, out var genre);

                    tracks.Add(new MusicMetadata(
                        Id: trackId.ToString(),
                        Title: title,
                        Artist: artist,
                        Album: album,
                        Genre: genre,
                        Duration: TimeSpan.FromMilliseconds(durationMs),
                        AlbumArtUri: albumArtUri?.ToString(),
                        IsExplicit: null,
                        ContentUri: contentUri?.ToString() ?? string.Empty,
                        Year: year > 0 ? year : null
                    ));
                }
            }

            return (IReadOnlyList<MusicMetadata>)tracks.AsReadOnly();
        });
    }

    public Task<string?> GetAlbumArtPathAsync(string trackId)
    {
        return Task.Run(() =>
        {
            if (!long.TryParse(trackId, out var id))
                return null;

            var cacheDir = Path.Combine(
                Application.Context.CacheDir!.AbsolutePath,
                "albumart"
            );
            var filePath = Path.Combine(cacheDir, $"{trackId}.jpg");
            if (File.Exists(filePath))
                return filePath;

            var projection = new[] { MediaStore.Audio.Media.InterfaceConsts.AlbumId };
            var selection = MediaStore.Audio.Media.InterfaceConsts.Id + " = ?";
            var selectionArgs = new[] { id.ToString() };

            using var cursor = Resolver.Query(
                MediaStore.Audio.Media.ExternalContentUri!, projection, selection, selectionArgs, null);

            if (cursor == null || !cursor.MoveToFirst())
                return null;

            var albumId = cursor.GetLong(0);
            var albumArtUri = ContentUris.WithAppendedId(
                Uri.Parse("content://media/external/audio/albumart")!, albumId
            );

            try
            {
                using var stream = Resolver.OpenInputStream(albumArtUri!);
                if (stream == null)
                    return null;

                Directory.CreateDirectory(cacheDir);
                using var fileStream = File.Create(filePath);
                stream.CopyTo(fileStream);
                return filePath;
            }
            catch
            {
                return null;
            }
        });
    }

    public Task<bool> HasStreamingSubscriptionAsync() => Task.FromResult(false);

    public async Task<PlaylistInfo> CreatePlaylistAsync(string name)
    {
        var playlist = await this.customPlaylists.CreateAsync(name);
        return new PlaylistInfo(playlist.Id, playlist.Name, 0);
    }

    public Task RemovePlaylistAsync(string playlistId)
        => this.customPlaylists.RemoveAsync(playlistId);

    public Task AddTrackToPlaylistAsync(string playlistId, MusicMetadata track)
        => this.customPlaylists.AddTrackAsync(playlistId, track);

    public Task RemoveTrackFromPlaylistAsync(string playlistId, string trackId)
        => this.customPlaylists.RemoveTrackAsync(playlistId, trackId);
}
