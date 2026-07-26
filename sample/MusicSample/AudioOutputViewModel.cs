using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;
using Shiny.Music;

namespace MusicSample;

[ShellMap<AudioOutputPage>("AudioOutput")]
public partial class AudioOutputViewModel(
    IAudioOutputDevices outputs,
    IMusicPlayer player
) : ObservableObject, IPageLifecycleAware
{
    // Set while pushing a platform-reported volume into VolumeValue, so the property-changed handler
    // doesn't turn the echo into a write back to the platform.
    bool syncingVolume;

    [ObservableProperty] string currentIcon = "🔈";
    [ObservableProperty] string currentName = "Unknown";
    [ObservableProperty] string currentType = "";
    [ObservableProperty] string currentId = "";
    [ObservableProperty] bool hasCurrent;

    [ObservableProperty] bool isWired;
    [ObservableProperty] bool isBluetooth;
    [ObservableProperty] bool isBuiltIn;
    [ObservableProperty] bool isHeadphones;
    [ObservableProperty] bool isExternalSystem;

    [ObservableProperty] double volumeValue;
    [ObservableProperty] string volumeText = "--";
    [ObservableProperty] bool canSetVolume;
    [ObservableProperty] string volumeNote = "";

    public ObservableCollection<AudioOutputItem> Outputs { get; } = [];
    public ObservableCollection<string> RouteLog { get; } = [];

    public void OnAppearing()
    {
        outputs.Changed += OnRouteChanged;
        player.VolumeChanged += OnVolumeChanged;

        CanSetVolume = player.IsVolumeControlSupported;
        VolumeNote = CanSetVolume
            ? "Drag to set the system media volume, or use the hardware buttons — either way VolumeChanged fires."
            : "Reading works everywhere, but Apple exposes no supported API to set the system volume, so the slider is read-only. Use the hardware buttons and watch it move.";

        Refresh();
        SyncVolume(player.Volume);
    }

    public void OnDisappearing()
    {
        outputs.Changed -= OnRouteChanged;
        player.VolumeChanged -= OnVolumeChanged;
    }

    // Both events arrive off the UI thread — the route callback from AudioDeviceCallback /
    // AVAudioSession, the volume one from a ContentObserver / KVO.
    void OnRouteChanged(object? sender, AudioOutputDevice? device)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            Log(device == null ? "Route changed — no output reported" : $"Route changed → {device.Name} ({device.Type})");
            Refresh();
        });

    void OnVolumeChanged(object? sender, float volume)
        => MainThread.BeginInvokeOnMainThread(() => SyncVolume(volume));

    [RelayCommand]
    void Refresh()
    {
        var current = outputs.Current;

        HasCurrent = current != null;
        CurrentName = current?.Name ?? "No output reported";
        CurrentType = current?.Type.ToString() ?? "";
        CurrentId = current == null ? "" : $"id: {current.Id}";
        CurrentIcon = IconFor(current);

        // The classification helpers — this is how an app should ask "is this wired?" rather than
        // switching on every AudioOutputType value.
        IsWired = current?.IsWired() == true;
        IsBluetooth = current?.IsBluetooth() == true;
        IsBuiltIn = current?.IsBuiltIn() == true;
        IsHeadphones = current?.IsHeadphones() == true;
        IsExternalSystem = current?.IsExternalSystem() == true;

        Outputs.Clear();
        foreach (var device in outputs.GetOutputs())
            Outputs.Add(new AudioOutputItem(IconFor(device), device.Name, device.Type.ToString(), FlagsFor(device), device.IsCurrent));
    }

    void SyncVolume(float volume)
    {
        this.syncingVolume = true;
        VolumeValue = volume;
        this.syncingVolume = false;
    }

    partial void OnVolumeValueChanged(double value)
    {
        VolumeText = $"{value * 100:0}%";

        if (this.syncingVolume || !CanSetVolume)
            return;

        // Android quantizes to the nearest STREAM_MUSIC step, so reading back can differ slightly.
        player.Volume = (float)value;
    }

    void Log(string message)
    {
        RouteLog.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (RouteLog.Count > 20)
            RouteLog.RemoveAt(RouteLog.Count - 1);
    }

    static string IconFor(AudioOutputDevice? device) => device switch
    {
        null => "🔇",
        _ when device.IsBluetooth() => "🎧",
        _ when device.IsWired() => "🔌",
        _ when device.IsExternalSystem() => "📺",
        _ => "🔈"
    };

    static string FlagsFor(AudioOutputDevice device)
    {
        var flags = new List<string>();
        if (device.IsWired()) flags.Add("wired");
        if (device.IsBluetooth()) flags.Add("bluetooth");
        if (device.IsBuiltIn()) flags.Add("built-in");
        if (device.IsHeadphones()) flags.Add("headphones");
        if (device.IsExternalSystem()) flags.Add("external");

        return flags.Count == 0 ? "unclassified" : String.Join(" · ", flags);
    }
}

public record AudioOutputItem(string Icon, string Name, string Type, string Flags, bool IsCurrent);
