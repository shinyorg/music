# Spotify App Remote bindings

.NET for Android & .NET for iOS bindings around Spotify's **App Remote** SDK, plus
the managed `ISpotifyRemote` abstraction the `MusicSample` app uses for playback.

App Remote **remote-controls the installed Spotify app** — your app tells Spotify
what to play; the audio comes out of the Spotify app. It is **not** an in-app audio
player.

## Requirements at runtime

- The **Spotify app installed** on the device (App Remote launches/authorizes it).
- A **Spotify Premium** account (free accounts cannot control playback).
- Your app's Client ID + redirect URI configured in `SpotifyConfig.cs` and registered
  in the [Spotify dashboard](https://developer.spotify.com/dashboard). Redirect URI:
  `musicsample://spotify-auth`.

## 1. Fetch the SDK binaries (not redistributed here)

The Android `.aar` and iOS `.xcframework` are Spotify's proprietary binaries and are
git-ignored. Download them into the projects:

```bash
bindings/fetch-spotify-sdks.sh
```

This places:
- `Shiny.Spotify.AppRemote.Android/Jars/spotify-app-remote-release-0.8.0.aar`
- `Shiny.Spotify.AppRemote.iOS/SpotifyiOS.xcframework`

## 2. Android binding — `Shiny.Spotify.AppRemote.Android`

Binds the App Remote `.aar` via `class-parse`. `Transforms/Metadata.xml` renames a
handful of `protocol.types` fields that collide with their class name (`Uri.uri`,
`Repeat.repeat`, …). Depends on `GoogleGson` (App Remote's default JSON mapper).

The App Remote AAR's manifest needs two placeholders, supplied by the **consuming
app** (already set in `MusicSample.csproj`):

```xml
<AndroidManifestPlaceholders>redirectSchemeName=musicsample;redirectHostName=spotify-auth</AndroidManifestPlaceholders>
```

Builds and links today (verified).

## 3. iOS binding — `Shiny.Spotify.AppRemote.iOS`

`NativeReference` to `SpotifyiOS.xcframework` with a hand-authored `ApiDefinition.cs`
covering the App Remote surface `ISpotifyRemote` uses (connect, play/pause/resume/
skip/seek, player-state updates). It is a **starting point** — if you extend it,
regenerate/verify with Objective Sharpie:

```bash
sharpie bind --sdk=iphoneos --namespace=Shiny.Spotify.AppRemote.iOS --output=. \
  SpotifyiOS.xcframework/ios-arm64/SpotifyiOS.framework/Headers/SpotifyiOS.h
```

Unlike Android, iOS authorization returns through the app's URL callback;
`AppDelegate.OpenUrl` forwards `musicsample://spotify-auth` to
`SpotifyRemoteApple.HandleAuthCallback`.

## 4. Managed abstraction

`sample/MusicSample/Spotify/ISpotifyRemote.cs` — the platform-neutral interface.
Implementations: `Platforms/Android/SpotifyRemoteAndroid.cs`,
`Platforms/iOS/SpotifyRemoteApple.cs`. Registered per-platform in `MauiProgram.cs`;
platforms without the SDK fall back to `UnavailableSpotifyRemote`.

Search and playlists still come from the Spotify **Web API** (`SpotifyClient`) — only
playback goes through App Remote.
