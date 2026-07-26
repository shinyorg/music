namespace Shiny.Music;

/// <summary>
/// Classification helpers over <see cref="AudioOutputType"/> so callers can ask "is this wired?" without
/// spelling out every variant the platforms report.
/// </summary>
public static class AudioOutputExtensions
{
    /// <summary>
    /// <c>true</c> for any physically cabled route: <see cref="AudioOutputType.WiredHeadphones"/>,
    /// <see cref="AudioOutputType.WiredHeadset"/> and <see cref="AudioOutputType.Usb"/>.
    /// </summary>
    /// <remarks>
    /// USB counts because on handsets without a 3.5&#160;mm jack the wired option <i>is</i> USB-C — Android
    /// reports those as <c>UsbHeadset</c>/<c>UsbDevice</c> and iOS reports digital USB-C headsets as
    /// <c>PortUsbAudio</c>, neither of which surfaces as a <c>Wired*</c> type. The trade-off is that a USB
    /// audio interface or DAC also answers <c>true</c>; use <see cref="IsHeadphones(AudioOutputType)"/> when
    /// you specifically mean something worn on the head, at the cost of missing USB-C earbuds.
    /// </remarks>
    public static bool IsWired(this AudioOutputType type) => type
        is AudioOutputType.WiredHeadphones
        or AudioOutputType.WiredHeadset
        or AudioOutputType.Usb;

    /// <inheritdoc cref="IsWired(AudioOutputType)"/>
    public static bool IsWired(this AudioOutputDevice device) => device.Type.IsWired();

    /// <summary>
    /// <c>true</c> for <see cref="AudioOutputType.Bluetooth"/> (HFP/SCO/LE) and
    /// <see cref="AudioOutputType.BluetoothA2dp"/>. Music normally routes over A2DP; the HFP variant shows up
    /// when a call or voice session has taken the link.
    /// </summary>
    public static bool IsBluetooth(this AudioOutputType type) => type
        is AudioOutputType.Bluetooth
        or AudioOutputType.BluetoothA2dp;

    /// <inheritdoc cref="IsBluetooth(AudioOutputType)"/>
    public static bool IsBluetooth(this AudioOutputDevice device) => device.Type.IsBluetooth();

    /// <summary>
    /// <c>true</c> for the device's own speaker or earpiece — i.e. nothing is plugged in or paired.
    /// </summary>
    public static bool IsBuiltIn(this AudioOutputType type) => type
        is AudioOutputType.BuiltInSpeaker
        or AudioOutputType.BuiltInReceiver;

    /// <inheritdoc cref="IsBuiltIn(AudioOutputType)"/>
    public static bool IsBuiltIn(this AudioOutputDevice device) => device.Type.IsBuiltIn();

    /// <summary>
    /// <c>true</c> for wired or Bluetooth headphones/headsets — the "audio is private to the user" check, e.g.
    /// before starting playback out loud. Excludes speakers, car audio and HDMI/AirPlay.
    /// </summary>
    /// <remarks>
    /// Bluetooth cannot be narrowed further: a paired A2DP route is equally a set of earbuds or a room speaker,
    /// and neither platform distinguishes them.
    /// </remarks>
    public static bool IsHeadphones(this AudioOutputType type) => type
        is AudioOutputType.WiredHeadphones
        or AudioOutputType.WiredHeadset
        or AudioOutputType.Bluetooth
        or AudioOutputType.BluetoothA2dp;

    /// <inheritdoc cref="IsHeadphones(AudioOutputType)"/>
    public static bool IsHeadphones(this AudioOutputDevice device) => device.Type.IsHeadphones();

    /// <summary>
    /// <c>true</c> when the route leaves the device entirely — car audio, HDMI or AirPlay. Useful to decide
    /// whether playback is "in the room" rather than on the handset.
    /// </summary>
    public static bool IsExternalSystem(this AudioOutputType type) => type
        is AudioOutputType.CarAudio
        or AudioOutputType.Hdmi
        or AudioOutputType.AirPlay;

    /// <inheritdoc cref="IsExternalSystem(AudioOutputType)"/>
    public static bool IsExternalSystem(this AudioOutputDevice device) => device.Type.IsExternalSystem();
}
