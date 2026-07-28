using Android.App;

// Permissions required by the media-playback foreground service that hosts background playback.
//
// Declared as assembly attributes rather than by an AndroidManifestOverlay in build/Shiny.Music.targets:
// a .targets file only reaches consumers who reference the NuGet package, so anyone consuming this project
// by ProjectReference (the sample, and anyone vendoring the source) would silently ship without them and
// crash the first time the service starts. Assembly attributes are scanned out of referenced assemblies
// during the app's manifest generation regardless of how the reference was made - the same mechanism that
// contributes the [Service] element on MusicPlaybackService.
//
// An app that sets MusicPlayerOptions.EnableBackgroundPlayback = false never starts the service and can
// strip these from its own manifest with the standard merger escape hatch:
//   <uses-permission android:name="android.permission.POST_NOTIFICATIONS" tools:node="remove" />

// Keeps the CPU alive while the screen is off. Without it the decode stalls in the user's pocket.
[assembly: UsesPermission(Android.Manifest.Permission.WakeLock)]

// Required to run any foreground service (API 28+).
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundService)]

// Required from Android 14 (API 34) for a service declaring foregroundServiceType="mediaPlayback".
[assembly: UsesPermission("android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK")]

// The media notification carries the transport controls. Runtime-requested on Android 13 (API 33)+;
// Shiny.Core's RequestForegroundServicePermissions() drives the prompt.
[assembly: UsesPermission(Android.Manifest.Permission.PostNotifications)]
