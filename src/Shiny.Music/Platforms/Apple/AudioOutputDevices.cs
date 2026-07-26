using AVFoundation;
using Foundation;

namespace Shiny.Music;

public class AudioOutputDevices : IAudioOutputDevices, IDisposable
{
    NSObject? routeObserver;

    public AudioOutputDevice? Current => this.GetOutputs().FirstOrDefault();

    public IReadOnlyList<AudioOutputDevice> GetOutputs()
    {
        // iOS/Catalyst surface only the *active* output route's ports - full discovery is owned by the OS route
        // picker (AVRoutePickerView / Control Center). So everything listed here is, by definition, current.
        var outputs = AVAudioSession.SharedInstance().CurrentRoute?.Outputs;
        if (outputs == null)
            return [];

        return outputs
            .Select(x => new AudioOutputDevice(x.UID, x.PortName, MapType(x.PortType), true))
            .ToList();
    }

    event EventHandler<AudioOutputDevice?>? changed;
    public event EventHandler<AudioOutputDevice?>? Changed
    {
        add
        {
            this.changed += value;
            this.EnsureObserver();
        }
        remove => this.changed -= value;
    }

    // Registered lazily on first subscription so simply resolving this service doesn't hook the audio session.
    void EnsureObserver() => this.routeObserver ??= AVAudioSession.Notifications.ObserveRouteChange(
        (_, _) => this.changed?.Invoke(this, this.Current)
    );

    public void Dispose()
    {
        this.routeObserver?.Dispose();
        this.routeObserver = null;
        GC.SuppressFinalize(this);
    }

    // PortType is a string constant, not an enum, so this is a comparison chain rather than a switch.
    static AudioOutputType MapType(string portType)
    {
        if (portType == AVAudioSession.PortBuiltInSpeaker) return AudioOutputType.BuiltInSpeaker;
        if (portType == AVAudioSession.PortBuiltInReceiver) return AudioOutputType.BuiltInReceiver;
        if (portType == AVAudioSession.PortHeadphones) return AudioOutputType.WiredHeadphones;
        // HeadsetMic is an *input* port - a mic-equipped wired headset still renders through Headphones - so
        // this never matches on an output route today. Mapped anyway so the classification can't go Unknown.
        if (portType == AVAudioSession.PortHeadsetMic) return AudioOutputType.WiredHeadset;
        if (portType == AVAudioSession.PortBluetoothA2DP) return AudioOutputType.BluetoothA2dp;
        if (portType == AVAudioSession.PortBluetoothHfp) return AudioOutputType.Bluetooth;
        if (portType == AVAudioSession.PortBluetoothLE) return AudioOutputType.Bluetooth;
        if (portType == AVAudioSession.PortUsbAudio) return AudioOutputType.Usb;
        if (portType == AVAudioSession.PortCarAudio) return AudioOutputType.CarAudio;
        if (portType == AVAudioSession.PortHdmi) return AudioOutputType.Hdmi;
        if (portType == AVAudioSession.PortAirPlay) return AudioOutputType.AirPlay;
        return AudioOutputType.Unknown;
    }
}
