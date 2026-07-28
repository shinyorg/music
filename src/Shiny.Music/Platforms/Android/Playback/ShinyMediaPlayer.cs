using Android.OS;
using AndroidX.Media3.Common;
using Google.Common.Util.Concurrent;
using Java.Util.Concurrent;
using Uri = Android.Net.Uri;

namespace Shiny.Music;

/// <summary>
/// Adapts <see cref="IMusicPlayer"/> to the Media3 <c>Player</c> contract so a <c>MediaSession</c> can drive
/// it - lock-screen and notification transport, headset and Bluetooth buttons, Android Auto, Wear.
/// <para>
/// <c>SimpleBasePlayer</c> is the designed base for wrapping an engine that is not ExoPlayer: we publish a
/// snapshot via <see cref="GetState"/> and the base class diffs it to raise the right listener callbacks.
/// ExoPlayer is deliberately NOT used as the engine - it cannot play DRM-protected catalog content, so the
/// engine has to stay behind <see cref="IPlaybackBackend"/> regardless.
/// </para>
/// </summary>
class ShinyMediaPlayer : SimpleBasePlayer
{
    // Everything we actually support. Commands left out are reported unavailable, so the notification and
    // lock screen hide the controls rather than showing dead buttons.
    static readonly PlayerCommands Commands = new PlayerCommands.Builder()!
        .AddAll(
            BasePlayer.InterfaceConsts.CommandPlayPause,
            BasePlayer.InterfaceConsts.CommandPrepare,
            BasePlayer.InterfaceConsts.CommandStop,
            BasePlayer.InterfaceConsts.CommandSeekInCurrentMediaItem,
            BasePlayer.InterfaceConsts.CommandGetCurrentMediaItem,
            BasePlayer.InterfaceConsts.CommandGetTimeline,
            BasePlayer.InterfaceConsts.CommandGetMetadata
        )!
        .Build()!;

    readonly IMusicPlayer player;
    readonly IMediaLibrary library;

    // Album art is resolved off the UI thread and cached per track: GetState() is called synchronously and
    // often, so it must never touch the file system.
    string? artworkTrackId;
    Uri? artworkUri;

    public ShinyMediaPlayer(IMusicPlayer player, IMediaLibrary library, Looper looper) : base(looper)
    {
        this.player = player;
        this.library = library;
    }

    /// <summary>Re-publishes the snapshot. Called whenever the underlying player's state moves.</summary>
    public void Invalidate() => this.InvalidateState();

    protected override State GetState()
    {
        var track = this.player.CurrentTrack;
        var state = this.player.State;

        var builder = new State.Builder()
            .SetAvailableCommands(Commands)!
            // Fully qualified: AndroidX.Media3.Common also exposes a PlaybackState, and the unqualified name
            // resolves to that one (a set of int constants) rather than Shiny's enum.
            .SetPlaybackState(state switch
            {
                Shiny.Music.PlaybackState.Playing or Shiny.Music.PlaybackState.Paused
                    => BasePlayer.InterfaceConsts.StateReady,
                _ => BasePlayer.InterfaceConsts.StateIdle
            })!
            .SetPlayWhenReady(
                state == Shiny.Music.PlaybackState.Playing,
                BasePlayer.InterfaceConsts.PlayWhenReadyChangeReasonUserRequest
            )!
            .SetContentPositionMs((long)this.player.Position.TotalMilliseconds)!;

        if (track != null)
        {
            builder.SetPlaylist(new List<MediaItemData> { this.BuildItem(track) })!
                .SetCurrentMediaItemIndex(0);
        }

        return builder.Build()!;
    }

    MediaItemData BuildItem(MusicMetadata track)
    {
        var metadata = new MediaMetadata.Builder()!
            .SetTitle(track.Title)!
            .SetArtist(track.Artist)!
            .SetAlbumTitle(track.Album)!
            .SetArtworkUri(this.ResolveArtwork(track))!
            .SetIsBrowsable(Java.Lang.Boolean.False)!
            .SetIsPlayable(Java.Lang.Boolean.True)!
            .Build()!;

        var item = new MediaItem.Builder()!
            .SetMediaId(track.Id)!
            .SetMediaMetadata(metadata)!
            .Build()!;

        // Duration is reported in microseconds; C.TimeUnset tells Media3 the scrubber length is unknown
        // (streaming items before they buffer).
        var durationUs = track.Duration > TimeSpan.Zero
            ? (long)(track.Duration.TotalMilliseconds * 1000)
            : C.TimeUnset;

        return new MediaItemData.Builder(track.Id)!
            .SetMediaItem(item)!
            .SetMediaMetadata(metadata)!
            .SetDurationUs(durationUs)!
            .SetIsSeekable(track.Duration > TimeSpan.Zero)!
            .SetIsDynamic(false)!
            .Build()!;
    }

    // GetState() runs synchronously on the app looper, so the artwork lookup (which hits the file system on
    // Apple and the ContentResolver here) is done once per track and cached. A miss just means no art on
    // this pass; the next Invalidate() picks it up.
    Uri? ResolveArtwork(MusicMetadata track)
    {
        if (this.artworkTrackId == track.Id)
            return this.artworkUri;

        this.artworkTrackId = track.Id;
        this.artworkUri = null;

        var task = this.library.GetAlbumArtPathAsync(track.Id);
        if (task.IsCompletedSuccessfully && !string.IsNullOrEmpty(task.Result))
            this.artworkUri = Uri.Parse(task.Result);
        else
            _ = task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !string.IsNullOrEmpty(t.Result) && this.artworkTrackId == track.Id)
                {
                    this.artworkUri = Uri.Parse(t.Result);
                    this.Invalidate();
                }
            }, TaskScheduler.Default);

        return this.artworkUri;
    }

    protected override IListenableFuture HandleSetPlayWhenReady(bool playWhenReady)
    {
        if (playWhenReady)
        {
            // Resume() is a no-op unless we are paused, so a play command on a stopped session does nothing
            // rather than throwing - the notification is dismissed at that point anyway.
            this.player.Resume();
        }
        else
        {
            this.player.Pause();
        }
        return ImmediateFuture.Instance;
    }

    protected override IListenableFuture HandleSeek(int mediaItemIndex, long positionMs, int seekCommand)
    {
        this.player.Seek(TimeSpan.FromMilliseconds(positionMs));
        return ImmediateFuture.Instance;
    }

    protected override IListenableFuture HandleStop()
    {
        this.player.Stop();
        return ImmediateFuture.Instance;
    }

    // Nothing to prepare - the backend loads on PlayAsync - but the command must be available for the
    // MediaSession to consider the player usable.
    protected override IListenableFuture HandlePrepare() => ImmediateFuture.Instance;

    protected override IListenableFuture HandleRelease() => ImmediateFuture.Instance;

    // COMMAND_SET_VOLUME is deliberately NOT advertised. Media3's player volume is a per-player attenuation,
    // whereas IMusicPlayer.Volume is the device-wide STREAM_MUSIC level - wiring one to the other would let
    // a remote controller (Auto, Wear, a notification slider) move the user's system volume. Ducking uses
    // the backend attenuation directly instead, which is not exposed through the session.

    /// <summary>
    /// An already-completed <c>ListenableFuture</c>. Guava's <c>Futures.immediateVoidFuture()</c> is not
    /// surfaced by the Xamarin binding (the ListenableFuture package binds only the interface), so the
    /// handful of lines it would have saved are implemented directly. Every operation here is synchronous,
    /// so the future is complete before it is returned.
    /// </summary>
    sealed class ImmediateFuture : Java.Lang.Object, IListenableFuture
    {
        public static readonly ImmediateFuture Instance = new();

        public void AddListener(Java.Lang.IRunnable? listener, IExecutor? executor)
        {
            if (listener != null && executor != null)
                executor.Execute(listener);
        }

        public bool Cancel(bool mayInterruptIfRunning) => false;
        public Java.Lang.Object? Get() => null;
        public Java.Lang.Object? Get(long timeout, TimeUnit? unit) => null;
        public bool IsCancelled => false;
        public bool IsDone => true;
    }
}
