using Android.Runtime;
using Com.Spotify.Android.Appremote.Api;
using Com.Spotify.Protocol.Client;
using Com.Spotify.Protocol.Types;
using Microsoft.Maui.ApplicationModel;
using JThrowable = Java.Lang.Throwable;
using JObject = Java.Lang.Object;

namespace Shiny.Spotify.Maui;

/// <summary>
/// Android App Remote implementation. Connects to the installed Spotify app and
/// drives its player. Wraps the Shiny.Spotify.AppRemote.Android binding.
/// </summary>
public class SpotifyRemoteAndroid : ISpotifyRemote
{
    SpotifyAppRemote? remote;
    Subscription? stateSubscription;

    public bool IsConnected => remote?.IsConnected == true;
    public event EventHandler<RemotePlayerState>? PlayerStateChanged;

    public Task ConnectAsync()
    {
        if (IsConnected)
            return Task.CompletedTask;

        var context = (Android.Content.Context?)Platform.CurrentActivity
                      ?? Android.App.Application.Context;

        var connectionParams = new ConnectionParams.Builder(SpotifyConfig.ClientId)
            .SetRedirectUri(SpotifyConfig.RedirectUri)
            .ShowAuthView(true)
            .Build();

        var tcs = new TaskCompletionSource();
        SpotifyAppRemote.Connect(context, connectionParams, new ConnectionListener(
            onConnected: r =>
            {
                remote = r;
                SubscribeState(r);
                tcs.TrySetResult();
            },
            onFailure: err => tcs.TrySetException(
                new InvalidOperationException(err.Message ?? "Could not connect to the Spotify app."))));

        return tcs.Task;
    }

    public void Disconnect()
    {
        stateSubscription?.Cancel();
        stateSubscription = null;
        if (remote != null)
        {
            SpotifyAppRemote.Disconnect(remote);
            remote = null;
        }
    }

    public Task PlayAsync(string spotifyUri) => Await(Player.Play(spotifyUri));
    public Task ResumeAsync() => Await(Player.Resume());
    public Task PauseAsync() => Await(Player.Pause());
    public Task SkipNextAsync() => Await(Player.SkipNext());
    public Task SkipPreviousAsync() => Await(Player.SkipPrevious());
    public Task SeekAsync(long positionMs) => Await(Player.SeekTo(positionMs));

    IPlayerApi Player => remote?.PlayerApi
        ?? throw new InvalidOperationException("Not connected to Spotify. Call ConnectAsync first.");

    void SubscribeState(SpotifyAppRemote r)
    {
        stateSubscription = r.PlayerApi.SubscribeToPlayerState();
        stateSubscription.SetEventCallback(new EventCallback(obj =>
        {
            var state = obj.JavaCast<PlayerState>();
            var track = state?.Track;
            PlayerStateChanged?.Invoke(this, new RemotePlayerState(
                track?.Name,
                track?.Artist?.Name,
                track?.Uri,
                state?.IsPaused ?? true,
                state?.PlaybackPosition ?? 0));
        }));
    }

    static Task Await(CallResult result)
    {
        var tcs = new TaskCompletionSource();
        result.SetResultCallback(new ResultCallback(_ => tcs.TrySetResult()));
        result.SetErrorCallback(new ErrorCallback(err => tcs.TrySetException(
            new InvalidOperationException(err.Message ?? "Spotify playback command failed."))));
        return tcs.Task;
    }

    // ── Java callback adapters ───────────────────────────────────────────────

    class ConnectionListener(Action<SpotifyAppRemote> onConnected, Action<JThrowable> onFailure)
        : JObject, IConnector.IConnectionListener
    {
        public void OnConnected(SpotifyAppRemote p0) => onConnected(p0);
        public void OnFailure(JThrowable p0) => onFailure(p0);
    }

    class ResultCallback(Action<JObject?> callback) : JObject, CallResult.IResultCallback
    {
        public void OnResult(JObject? p0) => callback(p0);
    }

    class ErrorCallback(Action<JThrowable> callback) : JObject, IErrorCallback
    {
        public void OnError(JThrowable p0) => callback(p0);
    }

    class EventCallback(Action<JObject> callback) : JObject, Subscription.IEventCallback
    {
        public void OnEvent(JObject p0) => callback(p0);
    }
}
