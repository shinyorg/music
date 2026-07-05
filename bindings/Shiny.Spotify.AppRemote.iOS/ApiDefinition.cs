using System;
using Foundation;
using ObjCRuntime;

// ─────────────────────────────────────────────────────────────────────────────
// Hand-authored binding for the subset of the Spotify iOS App Remote SDK used by
// ISpotifyRemote (connect, play/pause/resume/skip/seek, player-state updates).
//
// This is a STARTING POINT. If you extend it, the reliable path is to run
// Objective Sharpie against the SDK headers:
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
    ISPTAppRemoteDelegate Delegate { get; set; }

    [NullAllowed, Export("delegate", ArgumentSemantic.Weak)]
    NSObject WeakDelegate { get; set; }

    [Export("connected")]
    bool Connected { [Bind("isConnected")] get; }

    [Export("playerAPI")]
    [NullAllowed]
    ISPTAppRemotePlayerAPI PlayerAPI { get; }

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
    [Abstract]
    [Export("appRemoteDidEstablishConnection:")]
    void DidEstablishConnection(SPTAppRemote appRemote);

    [Abstract]
    [Export("appRemote:didFailConnectionAttemptWithError:")]
    void DidFailConnectionAttempt(SPTAppRemote appRemote, [NullAllowed] NSError error);

    [Abstract]
    [Export("appRemote:didDisconnectWithError:")]
    void DidDisconnect(SPTAppRemote appRemote, [NullAllowed] NSError error);
}

[Protocol]
[BaseType(typeof(NSObject))]
interface SPTAppRemotePlayerAPI
{
    [Wrap("WeakDelegate")]
    [NullAllowed]
    ISPTAppRemotePlayerStateDelegate Delegate { get; set; }

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
    [Abstract]
    [Export("playerStateDidChange:")]
    void PlayerStateDidChange(ISPTAppRemotePlayerState playerState);
}

[Protocol]
[BaseType(typeof(NSObject))]
interface SPTAppRemotePlayerState
{
    [Abstract]
    [Export("track")]
    ISPTAppRemoteTrack Track { get; }

    [Abstract]
    [Export("playbackPosition")]
    nint PlaybackPosition { get; }

    [Abstract]
    [Export("paused")]
    bool Paused { [Bind("isPaused")] get; }
}

[Protocol]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteTrack
{
    [Abstract]
    [Export("name")]
    string Name { get; }

    [Abstract]
    [Export("URI")]
    string Uri { get; }

    [Abstract]
    [Export("artist")]
    ISPTAppRemoteArtist Artist { get; }

    [Abstract]
    [Export("album")]
    ISPTAppRemoteAlbum Album { get; }

    [Abstract]
    [Export("duration")]
    nuint Duration { get; }
}

[Protocol]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteArtist
{
    [Abstract]
    [Export("name")]
    string Name { get; }

    [Abstract]
    [Export("URI")]
    string Uri { get; }
}

[Protocol]
[BaseType(typeof(NSObject))]
interface SPTAppRemoteAlbum
{
    [Abstract]
    [Export("name")]
    string Name { get; }

    [Abstract]
    [Export("URI")]
    string Uri { get; }
}
