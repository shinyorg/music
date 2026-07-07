# Apple Music on Android — Binding Feasibility Plan

**Status:** Research only. Not started. Revisit later.
**Date:** 2026-07-07

## Question

Does Apple Music offer an SDK giving Android functionality similar to what Spotify's
Android SDK gives us (the thing `Shiny.Spotify.Maui` wraps)?

## Answer: Yes — MusicKit for Android — but with big caveats

Apple's cross-platform equivalent to MusicKit-native is **MusicKit for Android**, shipped as
**two raw `.aar` files** (not a Maven/Gradle package):

- **`mediaplayback-release-1.1.x.aar`** — the player.
  - `com.apple.android.music.playback.controller` — `MediaPlayerController` you drive directly.
  - `com.apple.android.music.playback.queue` — `CatalogPlaybackQueueItemProvider.Builder` builds
    a queue from catalog IDs / containers.
  - `com.apple.android.music.playback.model` — playback state/model types.
- **`musickitauth-release-1.1.x.aar`** — `com.apple.android.sdk.authentication`.
  - Gets a **user token** (prompts sign-in, offers to install the Apple Music app if missing).
  - Token is used for playback and for Apple Music **Web API** REST calls (with a developer JWT).

## Architectural difference vs Spotify (important)

Spotify's **App Remote** model = your app is a thin remote controlling the *separately running*
Spotify app over IPC.

Apple's model is the **inverse**: with MusicKit, **your app IS the player** (subscription-backed),
in-process. There is **no "remote-control the Apple Music app"** equivalent. This mirrors the
Apple-native MusicKit we already bind in `macios/ShinyMusicKit.Binding` (there it's
`ApplicationMusicPlayer`; on Android it's `MediaPlayerController` + `CatalogPlaybackQueueItemProvider`).

Capability coverage is otherwise broadly comparable: auth, catalog search (via Web API), library
access, queue + playback control.

## Maturity / maintenance — the real risk

- **Not on Maven Central.** Must vendor the `.aar` files manually. A 2023 request to publish to
  Maven got **zero replies / no Apple response**.
- **Javadoc stamped May 31, 2019** (Java 1.8); artifacts have only crept to ~1.1.1 / 1.1.2.
  Effectively **stale / maintenance-at-best**.
- Ongoing developer friction reported (e.g. can't be consumed cleanly as a local `.aar` when the
  consumer is itself building an `.aar`; open compilation-error threads).

**Take:** it works and gives real Apple Music playback on Android, but expect rough edges and little
upstream support — noticeably behind both the Apple-native MusicKit and the Spotify Android SDK.

## Fit with this repo (how we'd do it)

Structurally **identical to the existing Spotify binding setup** — a proprietary, non-redistributable
vendor `.aar` fetched/vendored locally and wrapped per-platform:

1. **Bindings** — bind the two AARs, mirroring `bindings/Shiny.Spotify.AppRemote.Android`.
   - Likely a **separate per-platform project** again (won't multi-target cleanly), `IsPackable=false`.
   - **Redistribution constraint = same as Spotify:** cannot ship Apple's binary. Reuse the
     fetch-at-build-time pattern (`bindings/fetch-spotify-sdks.sh` + `InitialTargets` hook guarded by
     `!Exists(...)`).
2. **Cross-platform library** — add an Apple-Music-Android implementation behind the existing
   `ISpotifyRemote`-style abstraction (or a new sibling interface), mapping our unified surface onto
   `MediaPlayerController` / `CatalogPlaybackQueueItemProvider`. Note the *you-are-the-player* semantics
   differ from Spotify's remote model, so the interface mapping is not 1:1.
3. **Posture:** scope as **experimental / internal**, same as Spotify — do NOT package, document, or
   mention in readme/docs/skill/release notes until an explicit decision to ship.

## Open questions for when we return

- Latest AAR versions (were ~1.1.1 / 1.1.2 as of 2023) — recheck before committing.
- Developer-token (JWT) lifecycle + storage strategy shared with any Apple Music Web API usage.
- Whether the Android `.aar` binds cleanly with .NET for Android (Xamarin.Android binding) or needs
  metadata fixups — this is usually where the effort goes.
- Whether to unify Apple Music (native + Android) and Spotify under one abstraction or keep separate.

## Sources

- https://developer.apple.com/musickit/android/ — Android MusicKit overview / Javadoc
- https://developer.apple.com/musickit/ — MusicKit landing
- https://developer.apple.com/forums/thread/741338 — "publish Android SDK to Maven?" (no Apple reply)
- https://developer.apple.com/forums/tags/musickit — open MusicKit issues
