# Shiny.Music

[![NuGet](https://img.shields.io/nuget/v/Shiny.Music.svg)](https://www.nuget.org/packages/Shiny.Music/)

[![Documentation](https://img.shields.io/badge/docs-shinylib.net-blue)](https://shinylib.net/client/music)

A .NET library for accessing the device music library on **Android** and **iOS**. Provides a unified API for:

- 🔐 Requesting permissions to access music
- 🎵 Querying metadata about music on the device
- 🔎 Filtering tracks by genre, year, decade, and search text
- 📊 Browsing genres, years, and decades with track counts
- ▶️ Playing music files from the device library
- 🎧 Streaming Apple Music subscription tracks via `MPMusicPlayerController` (iOS)
- 📁 Copying music files (where permitted)
- 🔍 Checking for active streaming subscriptions

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

    public MyPage(IMediaLibrary library, IMusicPlayer player)
    {
        _library = library;
        _player = player;
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

        // 4. Browse genres with counts
        var genres = await _library.GetGenresAsync();

        // 5. Browse decades with counts
        var decades = await _library.GetDecadesAsync();

        // 6. Filter: Rock tracks from the 1990s
        var filtered = await _library.GetTracksAsync(new MusicFilter
        {
            Genre = "Rock",
            Decade = 1990
        });

        // 7. Cross-query: genres within the 2000s
        var genresIn2000s = await _library.GetGenresAsync(new MusicFilter { Decade = 2000 });

        // 8. Copy a track
        var dest = Path.Combine(FileSystem.AppDataDirectory, "copy.m4a");
        var success = await _library.CopyTrackAsync(tracks[0], dest);
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
- Music is queried through `MediaStore.Audio.Media`
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

> ⚠️ **This is mandatory.** Your app will crash on launch if you attempt to access the music library without this key.

#### Notes

- **Minimum iOS Version**: 15.0
- Permission is requested via `MPMediaLibrary.RequestAuthorization`
- Music metadata is queried using `MPMediaQuery` from the `MediaPlayer` framework
- **Local playback** uses `AVAudioPlayer` from `AVFoundation` with the item's `AssetURL`
- **Streaming playback** uses `MPMusicPlayerController.SystemMusicPlayer` for Apple Music subscription tracks with a `StoreId`
- `HasStreamingSubscriptionAsync()` checks `SKCloudServiceController` for the `MusicCatalogPlayback` capability
- **Copy Limitations**:
  - ✅ Locally synced / purchased (non-DRM) tracks can be exported via `AVAssetExportSession`
  - ❌ **Apple Music subscription (DRM-protected) tracks cannot be copied.** The `AssetURL` is empty for these items, and iOS does not provide filesystem access to DRM content.
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
| `GetTracksAsync(filter)` | Returns tracks matching a `MusicFilter` (genre, year, decade, search — combined with AND logic) |
| `GetGenresAsync(filter?)` | Returns distinct genres with track counts; optionally filtered by year/decade/search |
| `GetYearsAsync(filter?)` | Returns distinct release years with track counts; optionally filtered by genre/decade/search |
| `GetDecadesAsync(filter?)` | Returns distinct decades with track counts; optionally filtered by genre/year/search |
| `CopyTrackAsync(track, destPath)` | Copies a track to the specified path; returns `false` if not possible |
| `HasStreamingSubscriptionAsync()` | Checks for an active streaming subscription (iOS: Apple Music; Android: always `false`) |

### `MusicFilter`

All properties are optional and combined with AND logic. Pass to `GetTracksAsync`, `GetGenresAsync`, `GetYearsAsync`, or `GetDecadesAsync`.

| Property | Type | Description |
|---|---|---|
| `Genre` | `string?` | Filter by genre name (case-insensitive) |
| `Year` | `int?` | Filter by exact release year (takes precedence over `Decade`) |
| `Decade` | `int?` | Filter by decade start year (e.g., 1990 for the 1990s) |
| `SearchQuery` | `string?` | Text search across title, artist, and album |

### `GroupedCount<T>`

Returned by `GetGenresAsync`, `GetYearsAsync`, and `GetDecadesAsync`.

| Property | Type | Description |
|---|---|---|
| `Value` | `T` | The grouped value (`string` for genres, `int` for years/decades) |
| `Count` | `int` | Number of tracks in this group |

### `IMusicPlayer`

| Member | Description |
|---|---|
| `PlayAsync(track)` | Loads and plays the specified track (uses `AVAudioPlayer` or `MPMusicPlayerController` on iOS based on available URIs) |
| `Pause()` | Pauses current playback |
| `Resume()` | Resumes after pausing |
| `Stop()` | Stops playback and releases the track |
| `Seek(position)` | Seeks to a position in the track |
| `State` | Current `PlaybackState` (Stopped/Playing/Paused) |
| `CurrentTrack` | The currently loaded `MusicMetadata` |
| `Position` / `Duration` | Current position and total duration |
| `StateChanged` | Event fired when playback state changes |
| `PlaybackCompleted` | Event fired when a track finishes |

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
| `IsExplicit` | `bool?` | Whether the track is marked as explicit content. iOS only via `MPMediaItem.IsExplicitItem`; always `null` on Android. |
| `ContentUri` | `string` | URI used for playback and file operations |
| `StoreId` | `string?` | Apple Music catalog ID for streaming playback via `MPMusicPlayerController` (iOS only; `null` on Android) |
| `Year` | `int?` | Release year of the track, or `null` if not available. Android: `MediaStore.Audio.Media.YEAR`; iOS: derived from `MPMediaItem.ReleaseDate`. |

## Sample App

The `sample/MusicSample` project is a .NET MAUI app that demonstrates all library features:

1. **Permission Request** — Tap "Request Permission" to prompt for music access
2. **Browse** — Tap "Load All" to list all music on the device
3. **Search** — Type a query and tap "Search" to filter by title/artist/album
4. **Play/Pause/Stop** — Select a track and use the playback controls
5. **Copy** — Select a track and tap "Copy" to export it to app storage
6. **Genres** — Switch to the "Genres" tab to view all distinct genres with track counts
7. **Decades** — Switch to the "Decades" tab to view decades with track counts
8. **Years** — Switch to the "Years" tab to view release years with track counts

### Running the Sample

```bash
# Android
dotnet build sample/MusicSample -f net9.0-android -t:Run

# iOS (requires Mac with Xcode)
dotnet build sample/MusicSample -f net9.0-ios -t:Run
```

> **Note**: Music library access requires a physical device. Simulators/emulators typically have no music content.

## License

MIT
