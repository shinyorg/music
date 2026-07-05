using System;
using Foundation;
using ObjCRuntime;

// ─────────────────────────────────────────────────────────────────────────────
// Hand-authored binding for the subset of the Spotify iOS App Remote SDK used by
// ISpotifyRemote (connect, play/pause/resume/skip/seek, player-state updates).
//
// NOTE: the SDK's data/delegate types are ObjC @protocols, bound here as [Protocol, Model]
// and referenced by their PLAIN name (SPTAppRemoteTrack), never the I-prefixed interface.
// Two constraints force this shape:
//   * The binding tool's API-definition pre-compile only sees the types as declared and does
//     NOT have the generator-produced I<Protocol> interfaces, so the "pure protocol" style
//     (referencing ISPTAppRemoteTrack) fails to compile.
//   * Binding them as plain [BaseType(NSObject)] classes compiles, but the static registrar
//     then demands a native _OBJC_CLASS_$_SPTAppRemoteTrack symbol at link time — which does
//     not exist for a @protocol — so device builds fail with "Undefined symbols".
// [Protocol, Model] generates a concrete wrapper class (resolves by plain name in both compile
// passes) that is NOT a native-class wrapper (no _OBJC_CLASS_$_ reference), so it links too.
// We only consume/sub-class these objects, so a model binding is sufficient.
//
// If you extend it, run Objective Sharpie against the SDK headers:
//   sharpie bind --sdk=iphoneos --output=. \
//     --namespace=Shiny.Spotify.AppRemote.iOS \
//     SpotifyiOS.xcframework/ios-arm64/SpotifyiOS.framework/Headers/SpotifyiOS.h
// ─────────────────────────────────────────────────────────────────────────────

namespace Shiny.Spotify.AppRemote.iOS;

// void (^SPTAppRemoteCallback)(id _Nullable result, NSError * _Nullable error)
delegate void SPTAppRemoteCallback([NullAllowed] NSObject result, [NullAllowed] NSError error);

[BaseType(typeof(NSObject))]
interface SPTConfiguration
{
    [Export("initWithClientID:redirectURL:")]
    NativeHandle Constructor(string clientID, NSUrl redirectURL);

    [Export("clientID")]
    string ClientID { get; }

    [Export("redirectURL")]
    NSUrl RedirectURL { get; }

    [NullAllowed, Export("tokenSwapURL", ArgumentSemantic.Copy)]
    NSUrl TokenSwapURL { get; set; }

    [NullAllowed, Export("tokenRefreshURL", ArgumentSemantic.Copy)]
    NSUrl TokenRefreshURL { get; set; }
}

[BaseType(typeof(NSObject))]
interface SPTAppRemoteConnectionParams
{
    [NullAllowed, Export("accessToken")]
    string AccessToken { get; set; }
}

[BaseType(typeof(NSObject))]
interface SPTAppRemote
{
    [Export("initWithConfiguration:logLevel:")]
    NativeHandle Constructor(SPTConfiguration configuration, SPTAppRemoteLogLevel logLevel);

    [Export("connectionParameters")]
    SPTAppRemoteConnectionParams ConnectionParameters { get; }

    [Wrap("WeakDelegate")]
    [NullAllowed]
    SPTAppRemoteDelegate Delegate { get; set; }

    [NullAllowed, Export("delegate", ArgumentSemantic.Weak)]
    NSObject WeakDelegate { get; set; }

    [Export("connected")]
    bool Connected { [Bind("isConnected")] get; }

    [Export("playerAPI")]
    [NullAllowed]
    SPTAppRemotePlayerAPI PlayerAPI { get; }

    [Export("connect")]
    void Connect();

    [Export("disconnect")]
    void Disconnect();

    [Export("authorizeAndPlayURI:")]
    bool AuthorizeAndPlayUri(string uri);

    // Returns { "access_token": "...", "error_description": "..." } from the
    // musicsample://spotify-auth callback URL after the Spotify app authorizes.
    [Export("authorizationParametersFromURL:")]
    [return: NullAllowed]
    NSDictionary<NSString, NSString> AuthorizationParametersFromUrl(NSUrl url);
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteDelegate
{
    [Export("appRemoteDidEstablishConnection:")]
    void DidEstablishConnection(SPTAppRemote appRemote);

    [Export("appRemote:didFailConnectionAttemptWithError:")]
    void DidFailConnectionAttempt(SPTAppRemote appRemote, [NullAllowed] NSError error);

    [Export("appRemote:didDisconnectWithError:")]
    void DidDisconnect(SPTAppRemote appRemote, [NullAllowed] NSError error);
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemotePlayerAPI
{
    [Wrap("WeakDelegate")]
    [NullAllowed]
    SPTAppRemotePlayerStateDelegate Delegate { get; set; }

    [NullAllowed, Export("delegate", ArgumentSemantic.Weak)]
    NSObject WeakDelegate { get; set; }

    [Export("play:callback:")]
    void Play(string entityUri, [NullAllowed] SPTAppRemoteCallback callback);

    [Export("resume:")]
    void Resume([NullAllowed] SPTAppRemoteCallback callback);

    [Export("pause:")]
    void Pause([NullAllowed] SPTAppRemoteCallback callback);

    [Export("skipToNext:")]
    void SkipToNext([NullAllowed] SPTAppRemoteCallback callback);

    [Export("skipToPrevious:")]
    void SkipToPrevious([NullAllowed] SPTAppRemoteCallback callback);

    [Export("seekToPosition:callback:")]
    void SeekToPosition(nint position, [NullAllowed] SPTAppRemoteCallback callback);

    [Export("getPlayerState:")]
    void GetPlayerState([NullAllowed] SPTAppRemoteCallback callback);

    [Export("subscribeToPlayerState:")]
    void SubscribeToPlayerState([NullAllowed] SPTAppRemoteCallback callback);
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemotePlayerStateDelegate
{
    [Export("playerStateDidChange:")]
    void PlayerStateDidChange(SPTAppRemotePlayerState playerState);
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemotePlayerState
{
    [Export("track")]
    SPTAppRemoteTrack Track { get; }

    [Export("playbackPosition")]
    nint PlaybackPosition { get; }

    [Export("paused")]
    bool Paused { [Bind("isPaused")] get; }
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteTrack
{
    [Export("name")]
    string Name { get; }

    [Export("URI")]
    string Uri { get; }

    [Export("artist")]
    SPTAppRemoteArtist Artist { get; }

    [Export("album")]
    SPTAppRemoteAlbum Album { get; }

    [Export("duration")]
    nuint Duration { get; }
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteArtist
{
    [Export("name")]
    string Name { get; }

    [Export("URI")]
    string Uri { get; }
}

[Protocol, Model]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteAlbum
{
    [Export("name")]
    string Name { get; }

    [Export("URI")]
    string Uri { get; }
}
