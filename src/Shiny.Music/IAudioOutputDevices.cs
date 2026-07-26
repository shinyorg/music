namespace Shiny.Music;

/// <summary>
/// Reports the audio <b>output</b> route music is played through — the built-in speaker, wired or USB
/// headphones, a Bluetooth speaker, the car, HDMI or AirPlay — and raises <see cref="Changed"/> when that
/// route changes (headphones unplugged, Bluetooth speaker connects, …).
/// <para>
/// This is read-only: choosing the output is the user's job, through the OS route picker, Control Center or
/// the system volume/output UI. Nothing here needs a permission.
/// </para>
/// <para>
/// <b>Threading:</b> <see cref="Changed"/> is raised on whatever thread the OS delivers the route
/// notification on — marshal to the UI thread before touching UI state.
/// </para>
/// </summary>
public interface IAudioOutputDevices
{
    /// <summary>
    /// The output route currently in use, or <c>null</c> when the platform reports none.
    /// </summary>
    /// <remarks>
    /// On <b>Apple</b> this comes straight from <c>AVAudioSession.CurrentRoute</c> — the OS tells us the active
    /// route directly. On <b>Android</b> there is no "active" flag on <c>AudioDeviceInfo</c>, so the connected
    /// outputs are ranked the way the platform's own media-routing policy does (Bluetooth, then wired, then USB,
    /// then car/HDMI, then the built-in speaker, with the earpiece last) and the winner is reported. That is a
    /// very good approximation for media playback but it is a derived value, not a platform guarantee.
    /// </remarks>
    AudioOutputDevice? Current { get; }

    /// <summary>
    /// All output routes the OS currently reports, with <see cref="AudioOutputDevice.IsCurrent"/> set on the
    /// active one.
    /// </summary>
    /// <remarks>
    /// On <b>Android</b> this is the full list of connected outputs. On <b>Apple</b> the OS only exposes the
    /// <i>active</i> route's ports — discovery of everything else is owned by the system route picker — so this
    /// returns the current route (usually a single entry) and never the full set of reachable AirPlay/Bluetooth
    /// destinations. Use <see cref="Current"/> when you just need "where is the music going", and treat this as
    /// "what does the OS admit to" rather than a device picker.
    /// </remarks>
    IReadOnlyList<AudioOutputDevice> GetOutputs();

    /// <summary>
    /// Raised when the active output route changes. The argument is the new <see cref="Current"/> value, which
    /// may be <c>null</c>.
    /// </summary>
    event EventHandler<AudioOutputDevice?>? Changed;
}
