using Android.Content;
using Android.Media;

namespace Shiny.Music;

/// <summary>
/// What the system asked us to do with the audio focus we hold.
/// </summary>
enum AudioFocusEvent
{
    /// <summary>Permanent loss - another app took playback. Stop and abandon; there is no resume.</summary>
    Lost,

    /// <summary>Transient loss (a phone call). Pause, and resume on <see cref="Gained"/>.</summary>
    LostTransient,

    /// <summary>Transient loss that permits ducking (a navigation prompt). Lower, don't pause.</summary>
    Duck,

    /// <summary>Focus returned - restore volume and resume if we paused for a transient loss.</summary>
    Gained
}

/// <summary>
/// Wraps the <c>AudioManager</c> focus request. Without this the player is a bad citizen: another app
/// starting music won't pause us (two streams at once), we won't duck for navigation prompts, and we won't
/// pause for a call or resume after it.
/// </summary>
sealed class AudioFocusManager : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    readonly AudioManager audioManager;
    readonly AudioAttributes attributes;
    AudioFocusRequestClass? request;
    bool held;

    public AudioFocusManager()
    {
        this.audioManager = (AudioManager)Application.Context.GetSystemService(Context.AudioService)!;
        this.attributes = new AudioAttributes.Builder()!
            .SetContentType(AudioContentType.Music)!
            .SetUsage(AudioUsageKind.Media)!
            .Build()!;
    }

    public event EventHandler<AudioFocusEvent>? FocusChanged;

    /// <summary>Requests focus. Returns <c>true</c> when granted (or already held).</summary>
    public bool Request()
    {
        if (this.held)
            return true;

        AudioFocusRequest result;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            // SetWillPauseWhenDucked(false) keeps ducking in OUR hands: the system lowers nothing and
            // instead delivers AUDIOFOCUS_LOSS_TRANSIENT_CAN_DUCK, which we translate into an attenuation
            // on the active backend. That way an OS duck and an application Duck() are the same mechanism
            // and can be combined (lowest wins) rather than fighting each other.
            this.request = new AudioFocusRequestClass.Builder(AudioFocus.Gain)!
                .SetAudioAttributes(this.attributes)!
                .SetWillPauseWhenDucked(false)!
                .SetOnAudioFocusChangeListener(this)!
                .Build()!;

            result = this.audioManager.RequestAudioFocus(this.request);
        }
        else
        {
#pragma warning disable CA1422, CS0618   // the AudioFocusRequest overload is API 26+; this is the API 24-25 path
            result = this.audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CA1422, CS0618
        }

        this.held = result == AudioFocusRequest.Granted;
        return this.held;
    }

    public void Abandon()
    {
        if (!this.held)
            return;

        if (OperatingSystem.IsAndroidVersionAtLeast(26) && this.request != null)
        {
            this.audioManager.AbandonAudioFocusRequest(this.request);
            this.request.Dispose();
            this.request = null;
        }
        else
        {
#pragma warning disable CA1422, CS0618   // API 24-25 path
            this.audioManager.AbandonAudioFocus(this);
#pragma warning restore CA1422, CS0618
        }
        this.held = false;
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        var evt = focusChange switch
        {
            AudioFocus.Loss => AudioFocusEvent.Lost,
            AudioFocus.LossTransient => AudioFocusEvent.LostTransient,
            AudioFocus.LossTransientCanDuck => AudioFocusEvent.Duck,
            AudioFocus.Gain => AudioFocusEvent.Gained,
            _ => (AudioFocusEvent?)null
        };

        if (evt == null)
            return;

        // A permanent loss means the request is already dead on the system side; drop our handle so a
        // later Abandon() doesn't try to release focus we no longer hold.
        if (evt == AudioFocusEvent.Lost)
            this.held = false;

        this.FocusChanged?.Invoke(this, evt.Value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            this.Abandon();
        base.Dispose(disposing);
    }
}
