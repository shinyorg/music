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
- Fetching lyrics (plain text and synced LRC format)
- Retrieving album artwork
- Detecting the audio output route (speaker, wired/USB, Bluetooth, car, HDMI, AirPlay) and reacting when it changes
- Copying music files (where permitted)
- Checking for active streaming subscriptions
- Managing playlists — create, remove, and add/remove tracks via `IMediaLibrary`
- Automatic play count tracking (Apple platforms via MPMediaItem, Android via local storage)

## Installation

Add a project reference to `Shiny.Music` from your .NET MAUI or platform-specific app.

## Quick Start

```csharp
// Register in MauiProgram.cs
builder
    .UseMauiApp<App>()
    .UseShiny();          // required — Android permission checks run on Shiny.Core (see below)

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

        // 11b. Look up a single track / playlist by id (e.g. restoring saved state)
        var one = await _library.GetTrackByIdAsync(tracks[0].Id);
        var many = await _library.GetTracksByIdsAsync(new[] { tracks[0].Id, tracks[1].Id });
        var playlist = await _library.GetPlaylistByIdAsync(playlists[0].Id);

        // 12. Create a playlist and add tracks
        var newPlaylist = await _library.CreatePlaylistAsync("Favorites");
        await _library.AddTrackToPlaylistAsync(newPlaylist.Id, tracks[0]);

        // 13. Copy a track
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
- **Requires Shiny hosting** (**v4+**): runtime permission checks use **Shiny.Core**'s `AndroidPlatform`
  (`GetCurrentPermissionStatus` / `RequestAccess`). Call `.UseShiny()` in `MauiProgram` — or, for native
  Android, use `ShinyAndroidApplication` + `ShinyAndroidActivity` — so Shiny tracks the current activity and
  routes the permission result. Without Shiny hosting, `IMediaLibrary` cannot resolve `AndroidPlatform`.
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

#### Notes

- **Supported platforms**: iOS 17.0+, Mac Catalyst 17.0+
- Permission is requested via `MPMediaLibrary.RequestAuthorization`
- Music metadata is queried using `MPMediaQuery` (MediaPlayer framework)
- **Playback** uses `MPMusicPlayerController.ApplicationMusicPlayer` for all tracks — local items by persistent ID, and streaming catalog items by catalog id via `MPMusicPlayerStoreQueueDescriptor`
- `HasStreamingSubscriptionAsync()` checks MusicKit `MusicSubscription.GetCurrentAsync`
- **Catalog search** (`SearchCatalogAsync`) uses MusicKit `MusicCatalogSearchRequest` to search the Apple Music streaming catalog — results need not be in the user's library and are playable via `PlayAsync` when the user has an active subscription. The first call prompts for MusicKit authorization. Catalog tracks are streaming-only (empty `ContentUri`, not copyable).
- **Playlist management** uses locally-stored custom playlists (system playlists from `MPMediaQuery.PlaylistsQuery` are read-only)
- **Copy Limitations**:
  - Non-DRM tracks can be exported via `AVAssetExportSession`
  - **DRM-protected tracks cannot be copied.** `CopyTrackAsync` returns `false` for these.
  - Exported format is Apple M4A (`.m4a`)
---

## AI Tools

`Shiny.Music.Extensions.AI` exposes the music library and player as [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/) tool functions for LLM agents — so a chat agent can search and browse your library, pick a track for a mood, control playback, and manage playlists. Opt-in exactly which areas the model can see. Resolve `MusicAITools` from DI and pass `.Tools` to any `IChatClient`. AOT-compatible.

```bash
dotnet add package Shiny.Music.Extensions.AI
```

```csharp
using Shiny.Music.Extensions.AI;

builder.Services.AddShinyMusic();
builder.Services.AddMusicAITools(tools => tools
    .AddLibrary()             // search / browse / genres / playlists / lyrics (read-only)
    .AddPlayback()            // play, pause, resume, stop, seek, now-playing
    .AddPlaylistManagement()  // create / modify / delete custom playlists
);
// ...or simply .AddAll()  (the three cross-platform areas above)

// Apple Music catalog search is opt-in and Apple-only — guard it with a compiler flag:
// builder.Services.AddMusicAITools(tools =>
// {
//     tools.AddLibrary().AddPlayback().AddPlaylistManagement();
// #if IOS || MACCATALYST
//     tools.AddCatalog();   // exposes search_catalog (streaming catalog, not just the local library)
// #endif
// });

// later, hand the tools to a chat client
var tools = sp.GetRequiredService<MusicAITools>().Tools;
var response = await chatClient.GetResponseAsync(
    messages,
    new ChatOptions { Tools = [.. tools] }
);
```

Generated tools (only for areas you opt-in to):

| Area | Tools |
|---|---|
| `AddLibrary()` | `search_tracks`, `browse_tracks`, `list_music_categories`, `list_playlists`, `get_playlist_tracks`, `analyze_song_structure`, `get_lyrics` |
| `AddPlayback()` | `play_track` *(accepts `start_seconds`)*, `control_playback`, `get_now_playing` |
| `AddPlaylistManagement()` | `create_playlist`, `modify_playlist`, `delete_playlist` |
| `AddCatalog()` *(Apple-only, opt-in)* | `search_catalog` — searches the Apple Music streaming catalog; results are playable via `play_track`. Not in `AddAll()`; guard with `#if IOS \|\| MACCATALYST` |

`browse_tracks` filters by genre, year, decade, and free text — it's the natural path for "play me something mellow" or "pick an 80s track". The tools assume library permission is already granted — call `RequestPermissionAsync` from your app first; they do not trigger the permission UI.

`analyze_song_structure` lets an agent **start playback at a specific musical moment** — "play the famous guitar solo" or "skip to the final chorus". It returns the instrumental gaps (from time-synced lyrics) and audio-energy sections (from an offline scan) in seconds; the model uses its own knowledge of the song to pick the right one, then calls `play_track` with `start_seconds`. See [Audio Analysis & Song Structure](#audio-analysis--song-structure) below for the underlying APIs.

## Audio Analysis & Song Structure

Two APIs let you inspect a song **without playing it** — for drawing a waveform / VU meter, or for locating a specific part of a track (an intro, a chorus, a solo). This is what powers the `analyze_song_structure` AI tool.

**`AnalyzeLevelsAsync(trackId, window?)`** decodes a track to PCM offline (no audio is played) and measures its amplitude. It returns an `AudioLevels` with:

- `Rms` / `Peak` — per-window levels normalized `0.0–1.0` against the track's loudest sample (the "VU" envelope — one entry per `window`, default 500 ms).
- `Sections` — the envelope collapsed into contiguous `AudioSection` runs classified by relative `AudioEnergy` (`Silent`/`Quiet`/`Moderate`/`Loud`) — a compact "song structure" for finding, say, the loud instrumental stretch that is the solo.

It returns **`null`** for DRM-protected / streaming-only tracks that can't be decoded to PCM (the same tracks `CopyTrackAsync` refuses).

**`lyricsResult.GetInstrumentalGaps(duration?)`** derives the no-vocal stretches of a track from its time-synced (LRC) lyrics alone — the intro, breaks, solos, and outro. Because it needs **no audio decode, it works even for DRM tracks**. Combine the two: the lyric gaps give precise boundaries, the audio energy tells you which gap is the loud solo versus the quiet intro.

```csharp
// "Play the famous guitar solo from November Rain"
var track = (await library.SearchTracksAsync("November Rain")).First();

// 1. Precise instrumental boundaries from synced lyrics (DRM-safe)
var lyrics = await lyricsProvider.GetLyricsAsync(track);
var gaps = lyrics.GetInstrumentalGaps(track.Duration);      // e.g. intro, mid break, final solo

// 2. Energy sections to tell a loud solo from a quiet intro (null if DRM-protected)
var levels = await library.AnalyzeLevelsAsync(track.Id);
var solo = levels?.Sections
    .Where(s => s.Energy == AudioEnergy.Loud)
    .MaxBy(s => s.Start);                                   // the last big loud stretch — the outro solo

var startAt = solo?.Start ?? gaps.LastOrDefault()?.Start ?? TimeSpan.Zero;

// 3. Play from that point
await player.PlayAsync(track);
player.Seek(startAt);
```

### VU meter (`IVuMeter`)

`IMusicPlayer.CreateVuMeter(...)` returns an event-based VU meter for the current playback:

- **Android** — a **real audio-output tap** (`android.media.audiofx.Visualizer`) when the app holds the `RECORD_AUDIO` permission (`IsLive == true`). Add `<uses-permission android:name="android.permission.RECORD_AUDIO" />` and request it at runtime; without it the meter falls back to the implied version.
- **Apple** — the **implied** meter (`IsLive == false`): levels are synthesized from the `AudioLevels` analysis at the current playback position, because the system player (`MPMusicPlayerController`) exposes no output tap.

Either way you consume one plain event:

```csharp
var levels = await library.AnalyzeLevelsAsync(track.Id);   // needed for the implied (Apple) meter
var meter = player.CreateVuMeter(levels);                  // interval defaults to 50ms
meter.LevelChanged += (_, level) =>
{
    // level.Rms / level.Peak are 0.0–1.0; marshal to the UI thread before drawing
    UpdateVuBars(level.Rms, level.Peak);
};
meter.Start();
// ... later
meter.Dispose();
```

You can also sample a level for any position directly — `audioLevels.SampleAt(position)` returns a `VuLevel` — which is exactly what the implied meter does under the hood.

## Audio Output Route (`IAudioOutputDevices`)

`IAudioOutputDevices` tells you where the music is going — the built-in speaker, wired or USB headphones, a Bluetooth speaker, the car, HDMI or AirPlay — and raises `Changed` when that route changes (headphones unplugged, Bluetooth speaker connects). It is **output only** and **read-only**: choosing the output is the user's job through the OS route picker or Control Center. No permission is required.

```csharp
var outputs = serviceProvider.GetRequiredService<IAudioOutputDevices>();

var current = outputs.Current;                 // null when the platform reports no route
if (current != null)
{
    Console.WriteLine($"Playing on {current.Name} ({current.Type})");

    if (current.IsBluetooth())
        ShowBluetoothIcon();
    else if (current.IsWired())                // wired headphones/headset *or* USB-C/DAC
        ShowWiredIcon();
    else if (current.IsBuiltIn())
        ShowSpeakerIcon();
}

// Pause when the user yanks the headphones out and the audio falls back to the loudspeaker
outputs.Changed += (_, device) =>
{
    if (device?.IsBuiltIn() == true)
        MainThread.BeginInvokeOnMainThread(player.Pause);
};
```

`Changed` is raised on whatever thread the OS delivers the route notification on — marshal to the UI thread before touching UI state.

**Platform behaviour**

- **Android** — `GetOutputs()` returns every connected output. `AudioDeviceInfo` carries no "active" flag, so `Current` is *derived*: the connected outputs are ranked the way the platform's own media routing policy does (Bluetooth → wired → USB → car/HDMI → built-in speaker, earpiece last) and the winner is reported. A very good approximation for media playback, but not a platform guarantee.
- **Apple** — `Current` comes straight from `AVAudioSession.CurrentRoute`. The OS only exposes the *active* route's ports, so `GetOutputs()` returns that route (usually one entry) rather than every reachable AirPlay/Bluetooth destination — it is not a device picker. A mic-equipped wired headset renders as `WiredHeadphones`, since Apple only reports the headset mic on the input side.

## API Reference

### `IMediaLibrary`

| Method | Description |
|---|---|
| `RequestPermissionAsync()` | Prompts the user for music library access |
| `CheckPermissionAsync()` | Checks current permission status without prompting |
| `GetAllTracksAsync()` | Returns all music tracks on the device |
| `SearchTracksAsync(query)` | Searches tracks by title, artist, or album |
| `GetTracksAsync(filter)` | Returns tracks matching a `MusicFilter` (genre, year, decade, search -- combined with AND logic) |
| `GetTrackByIdAsync(trackId)` | Returns a single track by its identifier, or `null` if not found |
| `GetTracksByIdsAsync(trackIds)` | Returns multiple tracks by identifier in a single query, ordered to match the input (missing IDs omitted) |
| `GetGenresAsync(filter?)` | Returns distinct genres with track counts; optionally filtered by year/decade/search |
| `GetYearsAsync(filter?)` | Returns distinct release years with track counts; optionally filtered by genre/decade/search |
| `GetDecadesAsync(filter?)` | Returns distinct decades with track counts; optionally filtered by genre/year/search |
| `GetPlaylistsAsync()` | Returns all playlists with song counts, sorted alphabetically |
| `GetPlaylistByIdAsync(playlistId)` | Returns a single playlist (with song count) by its identifier, or `null` if not found |
| `GetPlaylistTracksAsync(playlistId)` | Returns all tracks in the specified playlist, in playlist order |
| `GetAlbumArtPathAsync(trackId)` | Returns a file path to album artwork for the track, or `null` |
| `AnalyzeLevelsAsync(trackId, window?)` | Decodes a track offline (**without playing it**) to per-window RMS/peak levels and energy `Sections` for a waveform/VU meter or locating a solo; returns `null` for DRM-protected tracks |
| `CopyTrackAsync(track, destPath)` | Copies a track to the specified path; returns `false` if not possible |
| `HasStreamingSubscriptionAsync()` | Checks for an active streaming subscription (Apple platforms: Apple Music; Android: always `false`) |
| `SearchCatalogAsync(term, limit)` | Searches the Apple Music streaming **catalog** (results need not be in the user's library); returns tracks playable via `PlayAsync`. **Apple only** — throws `PlatformNotSupportedException` on Android |
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
| `CreateVuMeter(implied?, interval?)` | Creates an `IVuMeter` (`LevelChanged` event) — a **real output tap** on Android (Visualizer, needs `RECORD_AUDIO`), or the **implied** meter (analysis synced to position) on Apple. Pass the `AnalyzeLevelsAsync` result for the implied meter |
| `State` | Current `PlaybackState` (Stopped/Playing/Paused) |
| `CurrentTrack` | The currently loaded `MusicMetadata` |
| `Position` / `Duration` | Current position and total duration |
| `Duck(options?)` | Lowers the playing music so an announcement can be heard over top; returns an `IAsyncDisposable` scope that restores full volume when disposed. Only one duck is active at a time — calling `Duck` while one is active returns a no-op scope |
| `IsDucked` | Whether a duck scope is currently active |
| `Volume` | Gets/sets the device media volume (0.0–1.0). **Reading works on all platforms.** Setting works on **Android** only; the **iOS/Mac Catalyst** setter throws `NotSupportedException` (Apple exposes no supported API to change system volume) — guard with `IsVolumeControlSupported` |
| `IsVolumeControlSupported` | Whether setting `Volume` is supported: `true` on Android, `false` on Apple platforms |
| `VolumeChanged` | Event (`float`, 0.0–1.0) fired when the device media volume changes — hardware buttons, Control Center, or a successful `Volume` set |
| `StateChanged` | Event fired when playback state changes |
| `PlaybackCompleted` | Event fired when a track finishes |

### `IAudioOutputDevices`

| Member | Description |
|---|---|
| `Current` | The `AudioOutputDevice` music is playing through, or `null`. Reported by the OS on Apple; derived from a route-priority ranking of connected outputs on Android |
| `GetOutputs()` | All output routes the OS reports, with `IsCurrent` set on the active one. Full list on Android; the active route only on Apple |
| `Changed` | Event (`AudioOutputDevice?`) fired when the active output route changes — may be raised on a background thread |

### `AudioOutputDevice`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Stable platform identifier (Apple port UID / Android `AudioDeviceInfo.Id`) |
| `Name` | `string` | Human-friendly name, e.g. "JBL Flip 5" or "Speaker" |
| `Type` | `AudioOutputType` | Normalized route type |
| `IsCurrent` | `bool` | Whether this is the route in use right now |

`AudioOutputType` values: `Unknown`, `BuiltInSpeaker`, `BuiltInReceiver`, `WiredHeadphones`, `WiredHeadset`, `Bluetooth` (HFP/SCO/LE), `BluetoothA2dp`, `Usb`, `CarAudio`, `Hdmi`, `AirPlay`.

Rather than switching on every value, use the `AudioOutputExtensions` helpers — each works on both `AudioOutputDevice` and `AudioOutputType`:

| Method | True for |
|---|---|
| `IsWired()` | `WiredHeadphones`, `WiredHeadset`, `Usb` — USB counts because on handsets with no 3.5mm jack the wired option *is* USB-C |
| `IsBluetooth()` | `Bluetooth`, `BluetoothA2dp` |
| `IsBuiltIn()` | `BuiltInSpeaker`, `BuiltInReceiver` — nothing plugged in or paired |
| `IsHeadphones()` | Wired or Bluetooth headphones/headsets — the "audio is private to the user" check |
| `IsExternalSystem()` | `CarAudio`, `Hdmi`, `AirPlay` — playback has left the device |

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
| `CatalogId` | `string?` | Apple Music **catalog** identifier, set on tracks from `SearchCatalogAsync`. When present, `PlayAsync` streams the track by catalog id (subscription required) and `ContentUri` is empty (not copyable). Null for local tracks and on Android. |

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

The **Audio Output** screen (Navigate → Audio Output) is a live demo of `IAudioOutputDevices` and the volume API: it shows the current route with its `IsWired()`/`IsBluetooth()`/`IsBuiltIn()`/`IsHeadphones()`/`IsExternalSystem()` classification, the full `GetOutputs()` list, a timestamped log of `Changed` events (plug/unplug to watch it fire), and the device media volume driven by `Volume`/`VolumeChanged` — with the slider disabled on Apple, where `IsVolumeControlSupported` is `false`. The player sheet also shows the current route under the track info.

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
