namespace Shiny.Music;

/// <summary>
/// Configuration for <see cref="IMusicPlayer"/>. Pass to <c>AddShinyMusic</c> to override the defaults.
/// </summary>
public class MusicPlayerOptions
{
    /// <summary>
    /// Whether playback continues when the app is backgrounded or the screen is off.
    /// <para>
    /// On <b>Android</b> this hosts the player in a media-playback foreground service with a
    /// <c>MediaSession</c>, giving you lock-screen and notification transport controls, headset and
    /// Bluetooth button handling, and protection from the low-memory killer. When <c>false</c>, playback
    /// runs in-process only and the OS may terminate it once the app is backgrounded.
    /// </para>
    /// <para>On <b>Apple</b> platforms this has no effect - <c>MPMusicPlayerController</c> is always
    /// out-of-process and continues in the background regardless.</para>
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool EnableBackgroundPlayback { get; set; } = true;

    /// <summary>
    /// <b>Android only.</b> The notification channel id used for the playback notification.
    /// Defaults to <c>shiny.music.playback</c>.
    /// </summary>
    public string NotificationChannelId { get; set; } = "shiny.music.playback";

    /// <summary>
    /// <b>Android only.</b> The user-visible name of the playback notification channel, shown in the
    /// system notification settings. Defaults to <c>Music Playback</c>.
    /// </summary>
    public string NotificationChannelName { get; set; } = "Music Playback";

    /// <summary>
    /// <b>Android only.</b> The drawable resource id for the playback notification's small icon.
    /// When <c>null</c>, Shiny.Core's <c>GetNotificationIconResource()</c> is used, which resolves the
    /// app's own notification icon.
    /// </summary>
    public int? NotificationIconResource { get; set; }

    /// <summary>
    /// Whether playback automatically resumes after a transient interruption ends - a phone call on either
    /// platform, or transient audio-focus loss on Android. When <c>false</c>, the player stays paused and
    /// the caller must call <see cref="IMusicPlayer.Resume"/>. Defaults to <c>true</c>.
    /// </summary>
    public bool AutoResumeAfterInterruption { get; set; } = true;

    /// <summary>
    /// <b>Android only.</b> Whether the player participates in system audio focus - pausing when another
    /// app takes playback, ducking for navigation prompts, and pausing for calls. Turning this off makes
    /// the player a bad citizen (two apps playing at once) and is only appropriate for kiosk-style apps
    /// that own the device audio. Defaults to <c>true</c>.
    /// </summary>
    public bool RespectAudioFocus { get; set; } = true;

    /// <summary>
    /// <b>Android only.</b> The attenuation applied when the system asks us to duck for a transient sound
    /// (a navigation prompt, a notification). Normalized 0.0-1.0. Defaults to 0.3.
    /// <para>
    /// This is distinct from <see cref="DuckOptions.Level"/>, which is the level for an application-initiated
    /// <see cref="IMusicPlayer.Duck"/>. When both are active the lower of the two wins, so neither can
    /// un-duck the other.
    /// </para>
    /// </summary>
    public double AudioFocusDuckLevel { get; set; } = 0.3;
}
