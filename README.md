# Shiny.Music

[![NuGet](https://img.shields.io/nuget/v/Shiny.Music.svg)](https://www.nuget.org/packages/Shiny.Music/)

[![Documentation](https://img.shields.io/badge/docs-shinylib.net-blue)](https://shinylib.net/client/music)

A .NET library for accessing the device music library on **Android**, **iOS**, and **Mac Catalyst**. Provides a unified API for:

- Requesting permissions to access music
- Querying metadata about music on the device
- Filtering tracks by genre, year, decade, and search text
- Browsing genres, years, and decades with track counts
- Browsing playlists and their tracks
- Playing music files from the device library
- Identifying songs by listening to audio (Apple platforms via ShazamKit)
- Fetching lyrics (plain text and synced LRC format)
- Retrieving album artwork
- Copying music files (where permitted)
- Checking for active streaming subscriptions
- Managing playlists — create, remove, and add/remove tracks via `IMediaLibrary`
- Automatic play count tracking (Apple platforms via MPMediaItem, Android via local storage)

## Installation

Add a project reference to `Shiny.Music` from your .NET MAUI or platform-specific app.

## Quick Start

```csharp
// Register in MauiProgram.cs
builder.Services.AddShinyMusic();

// Use via dependency injection
public class MyPage
{
    readonly IMediaLibrary _library;
    readonly IMusicPlayer _player;
    readonly ILyricsProvider _lyrics;

    public MyPage(IMediaLibrary library, IMusicPlayer player, ILyricsProvider lyrics)
    {
        _library = library;
        _player = player;
        _lyrics = lyrics;
    }

    async Task Example()
    {
        // 1. Request permission
        var status = await _library.RequestPermissionAsync();
        if (status != PermissionStatus.Granted) return;

        // 2. Get all tracks (includes PlayCount)
        var tracks = await _library.GetAllTracksAsync();

        // 3. Play a track
        await _player.PlayAsync(tracks[0]);

        // 4. Get album artwork
        var artPath = await _library.GetAlbumArtPathAsync(tracks[0].Id);

        // 5. Fetch lyrics
        var lyrics = await _lyrics.GetLyricsAsync(tracks[0]);

        // 6. Browse genres with counts
        var genres = await _library.GetGenresAsync();

        // 7. Browse decades with counts
        var decades = await _library.GetDecadesAsync();

        // 8. Filter: Rock tracks from the 1990s
        var filtered = await _library.GetTracksAsync(new MusicFilter
        {
            Genre = "Rock",
            Decade = 1990
        });

        // 9. Cross-query: genres within the 2000s
        var genresIn2000s = await _library.GetGenresAsync(new MusicFilter { Decade = 2000 });

        // 10. Browse playlists
        var playlists = await _library.GetPlaylistsAsync();

        // 11. Get tracks in a playlist
        var playlistTracks = await _library.GetPlaylistTracksAsync(playlists[0].Id);

        // 12. Create a playlist and add tracks
        var newPlaylist = await _library.CreatePlaylistAsync("Favorites");
        await _library.AddTrackToPlaylistAsync(newPlaylist.Id, tracks[0]);

        // 13. Copy a track
        var dest = Path.Combine(FileSystem.AppDataDirectory, "copy.m4a");
        var success = await _library.CopyTrackAsync(tracks[0], dest);

        // 14. Identify a song (Apple platforms only)
        var identifier = /* resolve IMusicIdentifier from DI */;
        var identified = await identifier.ListenAsync();
        if (identified != null)
            Console.WriteLine($"Identified: {identified.Title} by {identified.Artist}");
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

### Apple Platforms (iOS, Mac Catalyst)

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

- **Supported platforms**: iOS 15.0+, Mac Catalyst 15.0+
- Permission is requested via `MPMediaLibrary.RequestAuthorization`
- Music metadata is queried using `MPMediaQuery` (MediaPlayer framework)
- **Playback** uses `MPMusicPlayerController.ApplicationMusicPlayer` for all tracks
- `HasStreamingSubscriptionAsync()` checks MusicKit `MusicSubscription.GetCurrentAsync`
- **Playlist management** uses locally-stored custom playlists (system playlists from `MPMediaQuery.PlaylistsQuery` are read-only)
- **Copy Limitations**:
  - Non-DRM tracks can be exported via `AVAssetExportSession`
  - **DRM-protected tracks cannot be copied.** `CopyTrackAsync` returns `false` for these.
  - Exported format is Apple M4A (`.m4a`)
#### Entitlements

No special entitlements are required beyond the Info.plist usage description.

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
| `HasStreamingSubscriptionAsync()` | Checks for an active streaming subscription (Apple platforms: Apple Music; Android: always `false`) |
| `CreatePlaylistAsync(name)` | Creates a new playlist; returns `PlaylistInfo` |
| `RemovePlaylistAsync(playlistId)` | Removes a playlist |
| `AddTrackToPlaylistAsync(playlistId, track)` | Adds a track to a playlist (no-op if already present) |
| `RemoveTrackFromPlaylistAsync(playlistId, trackId)` | Removes a track from a playlist |

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
| `StateChanged` | Event fired when playback state changes |
| `PlaybackCompleted` | Event fired when a track finishes |

### `IMusicIdentifier`

| Member | Description |
|---|---|
| `ListenAsync(cancellationToken)` | Listens via microphone and returns a `MusicIdentificationResult`, or `null` if no match. Apple platforms only (ShazamKit). |

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
| `AlbumArtUri` | `string?` | Album art URI (Android: MediaStore content URI; Apple platforms: use `GetAlbumArtPathAsync` for cached file) |
| `IsExplicit` | `bool?` | Explicit content flag (Apple platforms only via `MPMediaItem.IsExplicitItem`; null on Android) |
| `ContentUri` | `string` | URI for playback/copy. Android: `content://` URI. Apple platforms: `ipod-library://` asset URL (empty for DRM tracks). |
| `StoreId` | `string?` | Track identifier for `MPMusicPlayerController` playback (Apple platforms only; null on Android) |
| `Year` | `int?` | Release year |
| `PlayCount` | `int` | Times played. Apple: from `MPMediaItem.PlayCount`. Android: from local store. Default 0. |

### `PlaylistInfo`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Platform-specific unique identifier for the playlist |
| `Name` | `string` | The display name of the playlist |
| `SongCount` | `int` | The number of tracks in the playlist |

### `GroupedCount<T>`

| Property | Type | Description |
|---|---|---|
| `Value` | `T` | The grouped value (`string` for genres, `int` for years/decades) |
| `Count` | `int` | Number of tracks in this group |

## Sample App

The `sample/MusicSample` project is a .NET MAUI app that demonstrates all library features including browsing, filtering, playback, album art display, and lyrics with synced highlighting.

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
