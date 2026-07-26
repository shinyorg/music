namespace Shiny.Music;

/// <summary>
/// Coarse classification of an audio <b>output</b> route, normalized across platforms. Maps from
/// <c>AVAudioSessionPortDescription.PortType</c> (Apple) and <c>AudioDeviceInfo.Type</c> (Android).
/// </summary>
/// <remarks>
/// Only render (outgoing) routes are modelled — this library plays the device music library, so capture
/// devices are out of scope. Use the <see cref="AudioOutputExtensions"/> helpers (<c>IsWired</c>,
/// <c>IsBluetooth</c>, <c>IsBuiltIn</c>, <c>IsHeadphones</c>) rather than switching on every member.
/// </remarks>
public enum AudioOutputType
{
    /// <summary>The platform reported a route this library does not classify (dock, line-out, remote submix, …).</summary>
    Unknown,

    /// <summary>The device's own loudspeaker.</summary>
    BuiltInSpeaker,

    /// <summary>The earpiece/receiver used for calls held to the ear.</summary>
    BuiltInReceiver,

    /// <summary>Wired headphones with no microphone (3.5&#160;mm or Lightning).</summary>
    WiredHeadphones,

    /// <summary>Wired headphones that also carry a microphone.</summary>
    WiredHeadset,

    /// <summary>Hands-free Bluetooth (HFP/SCO on Android, HFP/LE on Apple) — mic-capable, low quality.</summary>
    Bluetooth,

    /// <summary>Stereo A2DP Bluetooth — the normal route for music playback over Bluetooth.</summary>
    BluetoothA2dp,

    /// <summary>A USB audio device: USB-C earbuds, a headset, a DAC, or an audio interface.</summary>
    Usb,

    /// <summary>A car head unit (Android Auto / CarPlay / Bluetooth car audio reported as car audio).</summary>
    CarAudio,

    /// <summary>An HDMI-attached display or receiver.</summary>
    Hdmi,

    /// <summary>An AirPlay destination (Apple TV, HomePod, AirPlay 2 speaker). Apple platforms only.</summary>
    AirPlay
}

/// <summary>
/// A single audio <b>output</b> route as reported by the OS.
/// </summary>
/// <param name="Id">Stable platform identifier (Apple port UID / Android <c>AudioDeviceInfo.Id</c>).</param>
/// <param name="Name">Human-friendly name, e.g. "JBL Flip 5" or "Speaker".</param>
/// <param name="Type">Normalized route type.</param>
/// <param name="IsCurrent">
/// <c>true</c> when this is the route music is (or would be) played through right now. See
/// <see cref="IAudioOutputDevices.Current"/> for how each platform determines this.
/// </param>
public record AudioOutputDevice(
    string Id,
    string Name,
    AudioOutputType Type,
    bool IsCurrent
);
