using Foundation;
using Shiny.Spotify.AppRemote.iOS;

namespace Shiny.Spotify.Maui;

/// <summary>
/// iOS App Remote implementation. Connects to the installed Spotify app and drives
/// its player. Wraps the Shiny.Spotify.AppRemote.iOS binding.
///
/// NOTE: unlike Android, the iOS App Remote authorization returns through the app's
/// URL callback. AppDelegate.OpenUrl forwards the redirect to <see cref="HandleAuthCallback"/>.
/// This is verified for structure but not compiled here (needs SpotifyiOS.xcframework).
/// </summary>
public class SpotifyRemoteApple : NSObject, ISpotifyRemote
{
    SPTAppRemote? appRemote;
    TaskCompletionSource? connectTcs;
    ConnectionDelegate? connectionDelegate;
    PlayerStateDelegate? stateDelegate;

    public SpotifyRemoteApple() => Current = this;

    /// <summary>The live instance, so AppDelegate can route the auth callback URL.</summary>
    public static SpotifyRemoteApple? Current { get; private set; }

    public bool IsConnected => appRemote?.Connected == true;
    public event EventHandler<RemotePlayerState>? PlayerStateChanged;

    public Task ConnectAsync()
    {
        if (IsConnected)
            return Task.CompletedTask;

        var remote = EnsureRemote();
        connectTcs = new TaskCompletionSource();

        // Opens the Spotify app to authorize; the access token returns via the app's
        // URL callback (AppDelegate.OpenUrl -> HandleAuthCallback), which then connects.
        remote.AuthorizeAndPlayUri("");
        return connectTcs.Task;
    }

    public void Disconnect()
    {
        appRemote?.Disconnect();
        appRemote = null;
    }

    public Task PlayAsync(string spotifyUri) => Invoke(cb => Player.Play(spotifyUri, cb));
    public Task ResumeAsync() => Invoke(cb => Player.Resume(cb));
    public Task PauseAsync() => Invoke(cb => Player.Pause(cb));
    public Task SkipNextAsync() => Invoke(cb => Player.SkipToNext(cb));
    public Task SkipPreviousAsync() => Invoke(cb => Player.SkipToPrevious(cb));
    public Task SeekAsync(long positionMs) => Invoke(cb => Player.SeekToPosition((nint)positionMs, cb));

    ISPTAppRemotePlayerAPI Player => appRemote?.PlayerAPI
        ?? throw new InvalidOperationException("Not connected to Spotify. Call ConnectAsync first.");

    /// <summary>Called from AppDelegate.OpenUrl for the musicsample://spotify-auth redirect.</summary>
    public bool HandleAuthCallback(NSUrl url)
    {
        if (appRemote == null)
            return false;

        var parameters = appRemote.AuthorizationParametersFromUrl(url);
        var token = parameters?["access_token"]?.ToString();
        if (!string.IsNullOrEmpty(token))
        {
            appRemote.ConnectionParameters.AccessToken = token;
            appRemote.Connect();
            return true;
        }

        var error = parameters?["error_description"]?.ToString();
        if (!string.IsNullOrEmpty(error))
        {
            connectTcs?.TrySetException(new InvalidOperationException(error));
            return true;
        }
        return false; // not an App Remote callback (likely the Web API PKCE code)
    }

    SPTAppRemote EnsureRemote()
    {
        if (appRemote == null)
        {
            var config = new SPTConfiguration(SpotifyConfig.ClientId, new NSUrl(SpotifyConfig.RedirectUri));
            appRemote = new SPTAppRemote(config, SPTAppRemoteLogLevel.Info);
            connectionDelegate = new ConnectionDelegate(this);
            appRemote.Delegate = connectionDelegate;
        }
        return appRemote;
    }

    void OnConnected()
    {
        stateDelegate = new PlayerStateDelegate(this);
        var player = appRemote!.PlayerAPI!;
        player.Delegate = stateDelegate;
        player.SubscribeToPlayerState(null);
        connectTcs?.TrySetResult();
    }

    void OnConnectFailed(NSError? error)
        => connectTcs?.TrySetException(new InvalidOperationException(
            error?.LocalizedDescription ?? "Could not connect to the Spotify app."));

    void OnPlayerState(ISPTAppRemotePlayerState state)
    {
        var track = state.Track;
        PlayerStateChanged?.Invoke(this, new RemotePlayerState(
            track?.Name,
            track?.Artist?.Name,
            track?.Uri,
            state.Paused,
            state.PlaybackPosition));
    }

    static Task Invoke(Action<SPTAppRemoteCallback> action)
    {
        var tcs = new TaskCompletionSource();
        action((result, error) =>
        {
            if (error != null)
                tcs.TrySetException(new InvalidOperationException(error.LocalizedDescription));
            else
                tcs.TrySetResult();
        });
        return tcs.Task;
    }

    // ── ObjC delegate adapters ──────────────────────────────────────────────

    class ConnectionDelegate(SpotifyRemoteApple owner) : SPTAppRemoteDelegate
    {
        public override void DidEstablishConnection(SPTAppRemote appRemote) => owner.OnConnected();
        public override void DidFailConnectionAttempt(SPTAppRemote appRemote, NSError? error) => owner.OnConnectFailed(error);
        public override void DidDisconnect(SPTAppRemote appRemote, NSError? error) { }
    }

    class PlayerStateDelegate(SpotifyRemoteApple owner) : SPTAppRemotePlayerStateDelegate
    {
        public override void PlayerStateDidChange(ISPTAppRemotePlayerState playerState) => owner.OnPlayerState(playerState);
    }
}
