# Shiny.Music

[![NuGet](https://img.shields.io/nuget/v/Shiny.Music.svg)](https://www.nuget.org/packages/Shiny.Music/)

[![Documentation](https://img.shields.io/badge/docs-shinylib.net-blue)](https://shinylib.net/client/music)

A .NET library for accessing the device music library on **Android** and **iOS**. Provides a unified API for:

- Requesting permissions to access music
- Querying metadata about music on the device
- Filtering tracks by genre, year, decade, and search text
- Browsing genres, years, and decades with track counts
- Browsing playlists and their tracks
- Playing music files from the device library
- Controlling playback volume
- Streaming Apple Music subscription tracks via `MPMusicPlayerController` (iOS)
- Identifying songs by listening to audio (iOS via ShazamKit)
- Fetching lyrics (plain text and synced LRC format)
- Retrieving album artwork
- Copying music files (where permitted)
- Checking for active streaming subscriptions
- Managing custom playlists and play counts via `IMusicManager` (backed by SQLite)

## Installation

Add a project reference to `Shiny.Music` from your .NET MAUI or platform-specific app.

## Quick Start

```csharp
// Register in MauiProgram.cs
builder.Services.AddShinyMusic();
builder.Services.AddMusicManagementSqlite(); // Optional: custom playlists & play counts

// Use via dependency injection
public class MyPage
{
    readonly IMediaLibrary _library;
    readonly IMusicPlayer _player;
    readonly ILyricsProvider _lyrics;
    readonly IMusicManager _manager;

    public MyPage(IMediaLibrary library, IMusicPlayer player, ILyricsProvider lyrics, IMusicManager manager)
    {
        _library = library;
        _player = player;
        _lyrics = lyrics;
        _manager = manager;
    }

    async Task Example()
    {
        // 1. Request permission
        var status = await _library.RequestPermissionAsync();
        if (status != PermissionStatus.Granted) return;

        // 2. Get all tracks
        var tracks = await _library.GetAllTracksAsync();

        // 3. Play a track
        await _player.PlayAsync(tracks[0]);

        // 4. Control volume
        _player.Volume = 0.75f;

        // 5. Get album artwork
        var artPath = await _library.GetAlbumArtPathAsync(tracks[0].Id);

        // 6. Fetch lyrics
        var lyrics = await _lyrics.GetLyricsAsync(tracks[0]);

        // 7. Browse genres with counts
        var genres = await _library.GetGenresAsync();

        // 8. Browse decades with counts
        var decades = await _library.GetDecadesAsync();

        // 9. Filter: Rock tracks from the 1990s
        var filtered = await _library.GetTracksAsync(new MusicFilter
        {
            Genre = "Rock",
            Decade = 1990
        });

        // 10. Cross-query: genres within the 2000s
        var genresIn2000s = await _library.GetGenresAsync(new MusicFilter { Decade = 2000 });

        // 11. Browse playlists
        var playlists = await _library.GetPlaylistsAsync();

        // 12. Get tracks in a playlist
        var playlistTracks = await _library.GetPlaylistTracksAsync(playlists[0].Id);

        // 13. Copy a track
        var dest = Path.Combine(FileSystem.AppDataDirectory, "copy.m4a");
        var success = await _library.CopyTrackAsync(tracks[0], dest);

        // 14. Identify a song (iOS only)
        var identifier = /* resolve IMusicIdentifier from DI */;
        var identified = await identifier.ListenAsync();
        if (identified != null)
            Console.WriteLine($"Identified: {identified.Title} by {identified.Artist}");

        // 15. Track play counts
        await _manager.AddPlayCount(tracks[0].Id);
        var count = await _manager.GetPlayCount(tracks[0].Id);

        // 16. Custom playlists
        await _manager.CreatePlaylist("my-id", "Favorites");
        await _manager.AddTrackToPlaylist("my-id", tracks[0]);
        var customTracks = await _manager.GetPlaylistTracks("my-id");

        // 17. Browse all custom playlists
        var customPlaylists = await _manager.GetAllPlaylists();
    }
}
```

## Platform Configuration

### Android

#### Required Permissions

Add these to your `AndroidManifest.xml`:

```xml
<!-- Android 13+ (API 33+) -->
<uses-permission android:name="android.permission.READ_MEDIA_AUDIO" />

<!-- Android 12 and below (API < 33) -->
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE"
                 android:maxSdkVersion="32" />
```

#### Notes

- **Minimum API Level**: 24 (Android 7.0)
- **Target API 33+**: Uses `READ_MEDIA_AUDIO` granular media permission
- **Target API < 33**: Falls back to `READ_EXTERNAL_STORAGE`
- The library requests runtime permissions via the MAUI Permissions API
- Music is queried through `MediaStore.Audio.Media`; playlists through `MediaStore.Audio.Playlists`
- Playback uses `Android.Media.MediaPlayer` with content URIs
- `HasStreamingSubscriptionAsync()` always returns `false`
- **Copy**: Reads from the `ContentResolver` input stream. Works for all locally stored music files.

---

### iOS

#### Required Info.plist Entry

```xml
<key>NSAppleMusicUsageDescription</key>
<string>This app needs access to your music library to browse and play your music.</string>
```

> **This is mandatory.** Your app will crash on launch if you attempt to access the music library without this key.

For song identification via `IMusicIdentifier`, also add:

```xml
<key>NSMicrophoneUsageDescription</key>
<string>Used to identify songs playing nearby.</string>
```

#### Notes

- **Minimum iOS Version**: 15.0
- Permission is requested via `MPMediaLibrary.RequestAuthorization`
- Music metadata is queried using `MPMediaQuery` from the `MediaPlayer` framework; playlists via `MPMediaQuery.PlaylistsQuery`
- **Local playback** uses `AVAudioPlayer` from `AVFoundation` with the item's `AssetURL`
- **Streaming playback** uses `MPMusicPlayerController.SystemMusicPlayer` for Apple Music subscription tracks with a `StoreId`
- `HasStreamingSubscriptionAsync()` checks `SKCloudServiceController` for the `MusicCatalogPlayback` capability
- **Copy Limitations**:
  - Locally synced / purchased (non-DRM) tracks can be exported via `AVAssetExportSession`
  - **Apple Music subscription (DRM-protected) tracks cannot be copied.** The `AssetURL` is empty for these items, and iOS does not provide filesystem access to DRM content.
  - The `CopyTrackAsync` method returns `false` for tracks that cannot be exported.
  - Exported format is Apple M4A (`.m4a`)

#### Entitlements

No special entitlements are required beyond the Info.plist usage description. The `MediaPlayer` and `AVFoundation` frameworks are standard iOS frameworks.

---

## API Reference

### `IMediaLibrary`

| Method | Description |
|---|---|
| `RequestPermissionAsync()` | Prompts the user for music library access |
| `CheckPermissionAsync()` | Checks current permission status without prompting |
| `GetAllTracksAsync()` | Returns all music tracks on the device |
| `SearchTracksAsync(query)` | Searches tracks by title, artist, or album |
| `GetTracksAsync(filter)` | Returns tracks matching a `MusicFilter` (genre, year, decade, search -- combined with AND logic) |
| `GetGenresAsync(filter?)` | Returns distinct genres with track counts; optionally filtered by year/decade/search |
| `GetYearsAsync(filter?)` | Returns distinct release years with track counts; optionally filtered by genre/decade/search |
| `GetDecadesAsync(filter?)` | Returns distinct decades with track counts; optionally filtered by genre/year/search |
| `GetPlaylistsAsync()` | Returns all playlists with song counts, sorted alphabetically |
| `GetPlaylistTracksAsync(playlistId)` | Returns all tracks in the specified playlist, in playlist order |
| `GetAlbumArtPathAsync(trackId)` | Returns a file path to album artwork for the track, or `null` |
| `CopyTrackAsync(track, destPath)` | Copies a track to the specified path; returns `false` if not possible |
| `HasStreamingSubscriptionAsync()` | Checks for an active streaming subscription (iOS: Apple Music; Android: always `false`) |

### `IMusicPlayer`

| Member | Description |
|---|---|
| `PlayAsync(track)` | Loads and plays the specified track |
| `Pause()` | Pauses current playback |
| `Resume()` | Resumes after pausing |
| `Stop()` | Stops playback and releases the track |
| `Seek(position)` | Seeks to a position in the track |
| `State` | Current `PlaybackState` (Stopped/Playing/Paused) |
| `CurrentTrack` | The currently loaded `MusicMetadata` |
| `Position` / `Duration` | Current position and total duration |
| `Volume` | Playback volume from 0.0 to 1.0 (default 1.0) |
| `StateChanged` | Event fired when playback state changes |
| `PlaybackCompleted` | Event fired when a track finishes |

### `IMusicIdentifier`

| Member | Description |
|---|---|
| `ListenAsync(cancellationToken)` | Listens via microphone and returns a `MusicIdentificationResult`, or `null` if no match. iOS only (ShazamKit). |

### `MusicIdentificationResult`

| Property | Type | Description |
|---|---|---|
| `Title` | `string` | The title of the identified track |
| `Artist` | `string?` | Artist name |
| `Album` | `string?` | Album name |
| `Genre` | `string?` | Genre |
| `ArtworkUrl` | `string?` | URL to album/track artwork |
| `MusicUrl` | `string?` | URL to the track on a music streaming service |
| `Isrc` | `string?` | International Standard Recording Code |

### `ILyricsProvider`

| Method | Description |
|---|---|
| `GetLyricsAsync(track)` | Returns lyrics for the track, or `null` if unavailable |

### `LyricsResult`

| Property | Type | Description |
|---|---|---|
| `PlainLyrics` | `string?` | Plain text (unsynchronized) lyrics |
| `SyncedLyrics` | `string?` | Synchronized lyrics in LRC format with timestamps |

### `MusicFilter`

All properties are optional and combined with AND logic. Pass to `GetTracksAsync`, `GetGenresAsync`, `GetYearsAsync`, or `GetDecadesAsync`.

| Property | Type | Description |
|---|---|---|
| `Genre` | `string?` | Filter by genre name (case-insensitive) |
| `Year` | `int?` | Filter by exact release year (takes precedence over `Decade`) |
| `Decade` | `int?` | Filter by decade start year (e.g., 1990 for the 1990s) |
| `SearchQuery` | `string?` | Text search across title, artist, and album |

### `MusicMetadata`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Platform-specific unique identifier |
| `Title` | `string?` | Track title |
| `Artist` | `string?` | Artist name |
| `Album` | `string?` | Album name |
| `Genre` | `string?` | Genre (may be null) |
| `Duration` | `TimeSpan` | Track duration |
| `AlbumArtUri` | `string?` | Album art URI (Android only; null on iOS) |
| `IsExplicit` | `bool?` | Explicit content flag (iOS only; null on Android) |
| `ContentUri` | `string` | URI used for playback and file operations |
| `StoreId` | `string?` | Apple Music catalog ID for streaming (iOS only) |
| `Year` | `int?` | Release year |

### `PlaylistInfo`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Platform-specific unique identifier for the playlist |
| `Name` | `string` | The display name of the playlist |
| `SongCount` | `int` | The number of tracks in the playlist |

### `IMusicManager`

Custom playlist management and play count tracking, backed by SQLite via `Shiny.DocumentDb`. Registered with `AddMusicManagementSqlite()`.

| Method | Description |
|---|---|
| `AddPlayCount(trackId)` | Increments the play count for the specified track by one |
| `GetPlayCount(trackId)` | Gets the current play count, or 0 if never played |
| `GetAllPlayCounts()` | Returns all recorded play counts |
| `GetAllPlaylists()` | Returns all custom playlists with their track counts |
| `CreatePlaylist(playlistId, name)` | Creates a new playlist or updates the name of an existing one |
| `RemovePlaylist(playlistId)` | Removes a playlist and all of its associated tracks |
| `AddTrackToPlaylist(playlistId, metadata)` | Adds a track to a playlist (no-op if already present) |
| `GetPlaylistTracks(playlistId)` | Gets all tracks belonging to the specified playlist |

### `PlayCount`

| Property | Type | Description |
|---|---|---|
| `TrackId` | `string` | The platform-specific unique identifier for the track |
| `Count` | `int` | The total number of times the track has been played |

### `GroupedCount<T>`

| Property | Type | Description |
|---|---|---|
| `Value` | `T` | The grouped value (`string` for genres, `int` for years/decades) |
| `Count` | `int` | Number of tracks in this group |

## Sample App

The `sample/MusicSample` project is a .NET MAUI app that demonstrates all library features including browsing, filtering, playback with volume control, album art display, and lyrics with synced highlighting.

### Running the Sample

```bash
# Android
dotnet build sample/MusicSample -f net10.0-android -t:Run

# iOS (requires Mac with Xcode)
dotnet build sample/MusicSample -f net10.0-ios -t:Run
```

> **Note**: Music library access requires a physical device. Simulators/emulators typically have no music content.

## License

MIT
