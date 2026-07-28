using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Media3.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Shiny.Music;

/// <summary>
/// Hosts playback in a media-playback foreground service with a <c>MediaSession</c>.
/// <para>
/// Without this the player is only as durable as the process: <c>Android.Media.MediaPlayer</c> is not
/// Activity-scoped and does keep playing when the app backgrounds, but a backgrounded app with no
/// foreground service is a cached process and the low-memory killer takes it first - so playback stops
/// silently and unpredictably. Being a foreground service also brings the lock-screen transport,
/// the media notification, and headset/Bluetooth button handling.
/// </para>
/// <para>
/// Media3's <c>MediaSessionService</c> owns the foreground promotion and notification itself, so this does
/// NOT derive from Shiny.Core's <c>ShinyAndroidForegroundService</c> - the two would fight over the same
/// responsibility. Shiny.Core is still used for the runtime permission flow and service start/stop.
/// </para>
/// </summary>
// Fully qualified: Shiny.Core also defines a [Service] attribute (for DI registration), and an unqualified
// [Service] here resolves to that one instead of Android's.
[Android.App.Service(
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback
)]
[IntentFilter(new[] { "androidx.media3.session.MediaSessionService" })]
public class MusicPlaybackService : MediaSessionService
{
    MediaSession? session;
    ShinyMediaPlayer? mediaPlayer;
    MusicPlayer? owner;

    public override void OnCreate()
    {
        base.OnCreate();

        var services = Shiny.Hosting.Host.Current.Services;
        var player = services.GetRequiredService<IMusicPlayer>();
        var library = services.GetRequiredService<IMediaLibrary>();
        var options = services.GetRequiredService<MusicPlayerOptions>();

        this.EnsureNotificationChannel(options);

        this.mediaPlayer = new ShinyMediaPlayer(player, library, Looper.MainLooper!);

        var builder = new MediaSession.Builder(this, this.mediaPlayer)!
            .SetId("shiny.music")!;

        // Tapping the notification should reopen the app rather than doing nothing. Resolve the launcher
        // intent rather than hard-coding an Activity type, which a library cannot know.
        var launch = this.PackageManager?.GetLaunchIntentForPackage(this.PackageName!);
        if (launch != null)
        {
            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
            builder.SetSessionActivity(PendingIntent.GetActivity(this, 0, launch, flags)!);
        }

        this.session = builder.Build();

        // The Media3 player is a snapshot view; it must be told whenever the real player moves so the
        // notification and lock screen stay in step.
        if (player is MusicPlayer concrete)
        {
            this.owner = concrete;
            this.owner.SessionInvalidated += this.OnSessionInvalidated;
        }
    }

    void OnSessionInvalidated(object? sender, EventArgs e) => this.mediaPlayer?.Invalidate();

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo) => this.session;

    // The user swiped the app away. Media3 keeps the service alive by default so playback survives a swipe;
    // that is wrong for a library whose player is driven by the app, so stop when nothing is playing.
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        if (this.session?.Player?.PlayWhenReady != true)
        {
            this.StopSelf();
            return;
        }
        base.OnTaskRemoved(rootIntent);
    }

    public override void OnDestroy()
    {
        if (this.owner != null)
        {
            this.owner.SessionInvalidated -= this.OnSessionInvalidated;
            this.owner = null;
        }

        this.session?.Release();
        this.session = null;

        this.mediaPlayer?.Dispose();
        this.mediaPlayer = null;

        base.OnDestroy();
    }

    // Media3's DefaultMediaNotificationProvider posts to this channel id. Creating it here (rather than
    // letting Media3 create its own) is what makes MusicPlayerOptions.NotificationChannelName show up in
    // the system notification settings.
    void EnsureNotificationChannel(MusicPlayerOptions options)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var manager = (NotificationManager)this.GetSystemService(NotificationService)!;
        if (manager.GetNotificationChannel(options.NotificationChannelId) != null)
            return;

        var channel = new NotificationChannel(
            options.NotificationChannelId,
            options.NotificationChannelName,
            NotificationImportance.Low   // transport controls should never buzz or make a sound
        );
        channel.SetShowBadge(false);
        manager.CreateNotificationChannel(channel);
    }
}
