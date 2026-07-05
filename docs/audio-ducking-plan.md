# Implementation Plan: Audio Ducking for `IMusicPlayer`

## Goal

Let a caller temporarily lower the currently playing music so an announcement
(text-to-speech or a pre-recorded file) can be heard over top, then restore the
music. Primary scenario: play an audio announcement over music.

## Design decisions (settled)

- **Ducking lives on `IMusicPlayer`**, not a separate service. For the target
  scenario (announce over music this library is playing) the two edge cases that
  would justify a standalone service don't apply, so we keep the surface small.
- **No `Volume` / `CanControlVolume` members.** A numeric volume setter can't be
  honored for Apple Music streaming and would be a footgun. Ducking only.
- **Last-writer-wins**, no reference counting. A newer `Duck()` supersedes an
  older one; disposing the active scope restores full volume; disposing a
  superseded scope does nothing.
- **No-op when nothing is playing** (`State != Playing`).

## Known platform asymmetry (accepted, must be documented in XML docs)

| | Android | Apple |
|---|---|---|
| Mechanism | `Android.Media.MediaPlayer.SetVolume` on this player's track | `AVAudioSession` `.Playback` + `.DuckOthers` |
| Scope of duck | Only this library's track | All "other" (out-of-process) audio, incl. Apple Music |
| `Level` honored | Yes (exact) | No — OS-fixed depth |
| `FadeIn`/`FadeOut` honored | Yes | No — OS-controlled ramp |
| Works with Apple Music streaming | n/a | Yes |

**Apple exemption (the important part):** `DuckOthers` only ducks audio that is
"other" relative to the app's `AVAudioSession`. Anything playing *through the app
session* — an `AVAudioPlayer` announcement file, or `AVSpeechSynthesizer` TTS
with `usesApplicationAudioSession = true` (the default) — is **not** ducked and
plays at full volume over the ducked music. Third-party audio (podcast, Spotify)
would be ducked too, but the deliberately-played announcement never is.

## Public API

Additions to `src/Shiny.Music/IMusicPlayer.cs`:

```csharp
/// <summary>True while a duck scope is active.</summary>
bool IsDucked { get; }

/// <summary>
/// Lowers the currently playing music so an announcement can be heard over top,
/// until the returned scope is disposed. Last-writer-wins: a newer Duck supersedes
/// an older one; disposing the active scope restores full volume.
/// Android: lowers this player's track (Level and fades honored).
/// Apple: activates AVAudioSession .DuckOthers (Level and fades are advisory — the
/// OS controls duck depth and ramp). Does not duck your own announcement audio when
/// it plays through the app audio session.
/// No-op if nothing is currently playing.
/// </summary>
IAsyncDisposable Duck(DuckOptions? options = null);
```

New file `src/Shiny.Music/DuckOptions.cs`:

```csharp
namespace Shiny.Music;

public record DuckOptions
{
    /// <summary>Target volume 0.0-1.0 while ducked. Android: exact. Apple: ignored (OS-fixed).</summary>
    public double Level { get; init; } = 0.2;

    /// <summary>Ramp down when ducking starts. Android: honored. Apple: ignored.</summary>
    public TimeSpan FadeIn { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Ramp back up when the scope disposes. Android: honored. Apple: ignored.</summary>
    public TimeSpan FadeOut { get; init; } = TimeSpan.FromMilliseconds(200);
}
```

Internal helper (private nested type in each platform player is fine):

```csharp
// Returned from Duck(); disposal restores volume iff still the active scope.
sealed class DuckScope(Func<DuckScope, ValueTask> onDispose) : IAsyncDisposable { ... }
```

## Usage

```csharp
await using (player.Duck(new DuckOptions { Level = 0.15 }))
    await speech.SpeakAsync("Now boarding at gate 12");
// music restored on dispose
```

## Implementation — Android (`Platforms/Android/MusicPlayer.cs`)

- Track `IsDucked` and the current active `DuckScope` (last-writer-wins → a single
  field; opening a new scope replaces it).
- `Duck()`:
  - If `player == null` / not playing → return a no-op scope.
  - Capture the pre-duck volume (nominal `1.0`).
  - Ramp `SetVolume(cur, cur)` from current → `Level` over `FadeIn` via a timer
    (e.g. `System.Timers.Timer` or a short loop on a background task). Keep it simple.
  - Set `IsDucked = true`.
  - Return a `DuckScope` whose disposal, *if it is still the active scope*, ramps
    volume back to `1.0` over `FadeOut` and sets `IsDucked = false`.
- Ensure `Stop()` clears any active duck state so a new track starts at full volume.

## Implementation — Apple (`Platforms/Apple/MusicPlayer.cs`)

- `Duck()`:
  - If not playing → no-op scope.
  - `AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Playback,
    AVAudioSessionCategoryOptions.DuckOthers)` then `SetActive(true)`.
  - Set `IsDucked = true`. `Level`/fades are ignored (documented).
  - Return a `DuckScope` whose disposal, if still active, calls
    `SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation)`
    and sets `IsDucked = false`.
- Add an XML remark on `PlayAsync`/the class noting that for the announcement to
  play at full volume it must share the app audio session (AVAudioPlayer does so
  automatically; `AVSpeechSynthesizer` must keep `usesApplicationAudioSession = true`).
- Guard the `SetCategory`/`SetActive` calls (they return `NSError`); log/ignore
  failures rather than throw from `Duck()`.

## Registration

**No change.** `ServiceCollectionExtensions` stays as-is — ducking is part of the
existing `IMusicPlayer` registration.

## Files touched

- `src/Shiny.Music/IMusicPlayer.cs` — add `IsDucked`, `Duck(...)`.
- `src/Shiny.Music/DuckOptions.cs` — new.
- `src/Shiny.Music/Platforms/Android/MusicPlayer.cs` — implement.
- `src/Shiny.Music/Platforms/Apple/MusicPlayer.cs` — implement.
- (optional) sample app: a button that ducks and plays an announcement.

## Verification

- **Android:** play a track, `Duck(0.15)`, confirm audible drop + fade, dispose,
  confirm restore. Open a second overlapping `Duck` → confirm last-writer-wins.
- **Apple (device, real Apple Music track):** play streaming track, `Duck()`,
  play an `AVAudioPlayer` announcement file → music dips, announcement full volume,
  music restores on dispose. Repeat with `AVSpeechSynthesizer` TTS.
- **No-op:** call `Duck()` while stopped → no exception, scope disposes cleanly.

## Out of scope

- Numeric per-track volume on Apple (impossible for DRM streaming).
- Hybrid `AVAudioEngine` path for local DRM-free assets.
- System-wide "duck other apps" behavior on Android via audio focus.
