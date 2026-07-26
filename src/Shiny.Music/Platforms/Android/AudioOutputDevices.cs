using Android.Content;
using Android.Media;
using NativeAudioDeviceType = Android.Media.AudioDeviceType;

namespace Shiny.Music;

public class AudioOutputDevices : IAudioOutputDevices, IDisposable
{
    AudioManager? audioManager;
    DeviceCallback? callback;

    AudioManager AudioManager => this.audioManager ??=
        (AudioManager)Application.Context.GetSystemService(Context.AudioService)!;

    public AudioOutputDevice? Current => this.GetOutputs().FirstOrDefault(x => x.IsCurrent);

    public IReadOnlyList<AudioOutputDevice> GetOutputs()
    {
        var result = new List<AudioOutputDevice>();
        var devices = this.AudioManager.GetDevices(GetDevicesTargets.Outputs);
        if (devices == null)
            return result;

        // "Current" is a derived value on Android - AudioDeviceInfo carries no active flag. Rank the connected
        // outputs the way the platform's own media routing policy does and take the winner. Match on device Id,
        // not type, so two routes of the same type don't both report as current.
        var currentId = devices
            .OrderBy(x => RoutePriority(MapType(x.Type)))
            .FirstOrDefault()
            ?.Id;

        foreach (var device in devices)
        {
            var type = MapType(device.Type);
            result.Add(new AudioOutputDevice(
                device.Id.ToString(),
                GetName(device, type),
                type,
                currentId != null && device.Id == currentId
            ));
        }
        return result;
    }

    event EventHandler<AudioOutputDevice?>? changed;
    public event EventHandler<AudioOutputDevice?>? Changed
    {
        add
        {
            this.changed += value;
            this.EnsureCallback();
        }
        remove => this.changed -= value;
    }

    // Registered lazily on first subscription (same pattern as MusicPlayer's volume observer) so simply
    // resolving this service doesn't hook the audio system.
    void EnsureCallback()
    {
        if (this.callback != null)
            return;

        this.callback = new DeviceCallback(() => this.changed?.Invoke(this, this.Current));
        this.AudioManager.RegisterAudioDeviceCallback(this.callback, null);
    }

    public void Dispose()
    {
        if (this.callback != null)
        {
            this.audioManager?.UnregisterAudioDeviceCallback(this.callback);
            this.callback.Dispose();
            this.callback = null;
        }
        GC.SuppressFinalize(this);
    }

    // AudioDeviceInfo.ProductName for the built-in routes is the handset model ("Pixel 7"), which reads oddly
    // as "playing on Pixel 7". Label those from the type and keep ProductName for actual accessories.
    static string GetName(AudioDeviceInfo device, AudioOutputType type) => type switch
    {
        AudioOutputType.BuiltInSpeaker => "Speaker",
        AudioOutputType.BuiltInReceiver => "Earpiece",
        _ => device.ProductName?.ToString() ?? type.ToString()
    };

    // Lower wins. An attached external transport takes the route from the built-ins; the earpiece is last
    // because it is only ever selected for in-call use, never as a media route.
    static int RoutePriority(AudioOutputType type) => type switch
    {
        AudioOutputType.BluetoothA2dp => 0,
        AudioOutputType.Bluetooth => 1,
        AudioOutputType.WiredHeadset => 2,
        AudioOutputType.WiredHeadphones => 3,
        AudioOutputType.Usb => 4,
        AudioOutputType.CarAudio => 5,
        AudioOutputType.Hdmi => 6,
        AudioOutputType.AirPlay => 7,
        AudioOutputType.BuiltInSpeaker => 8,
        AudioOutputType.BuiltInReceiver => 9,
        _ => 10
    };

    // CA1416 is suppressed across the whole map: several of these enum values were added after the minimum
    // supported API (USB in 26, SpeakerSafe in 30, the BLE routes in 31/33). Matching on them is safe at any
    // level - an older device simply never reports the value - and nothing here calls a version-gated API.
#pragma warning disable CA1416
    static AudioOutputType MapType(NativeAudioDeviceType type) => type switch
    {
        NativeAudioDeviceType.BuiltinSpeaker => AudioOutputType.BuiltInSpeaker,
        NativeAudioDeviceType.BuiltinSpeakerSafe => AudioOutputType.BuiltInSpeaker,
        NativeAudioDeviceType.BleHeadset => AudioOutputType.Bluetooth,
        NativeAudioDeviceType.BleSpeaker => AudioOutputType.BluetoothA2dp,
        NativeAudioDeviceType.BleBroadcast => AudioOutputType.BluetoothA2dp,
        NativeAudioDeviceType.BuiltinEarpiece => AudioOutputType.BuiltInReceiver,
        NativeAudioDeviceType.WiredHeadset => AudioOutputType.WiredHeadset,
        NativeAudioDeviceType.WiredHeadphones => AudioOutputType.WiredHeadphones,
        NativeAudioDeviceType.BluetoothA2dp => AudioOutputType.BluetoothA2dp,
        NativeAudioDeviceType.BluetoothSco => AudioOutputType.Bluetooth,
        NativeAudioDeviceType.UsbDevice => AudioOutputType.Usb,
        NativeAudioDeviceType.UsbHeadset => AudioOutputType.Usb,
        NativeAudioDeviceType.UsbAccessory => AudioOutputType.Usb,
        NativeAudioDeviceType.Hdmi => AudioOutputType.Hdmi,
        NativeAudioDeviceType.HdmiArc => AudioOutputType.Hdmi,
        // TYPE_BUS is how Android Automotive surfaces the head unit's own zones.
        NativeAudioDeviceType.Bus => AudioOutputType.CarAudio,
        _ => AudioOutputType.Unknown
    };
#pragma warning restore CA1416

    sealed class DeviceCallback(Action onChanged) : AudioDeviceCallback
    {
        public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices) => onChanged();
        public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices) => onChanged();
    }
}
