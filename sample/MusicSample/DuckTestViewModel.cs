using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using Shiny;
using Shiny.Music;
using PermissionStatus = Shiny.Music.PermissionStatus;

namespace MusicSample;

[ShellMap<DuckTestPage>(registerRoute: false)]
public partial class DuckTestViewModel(
    IMediaLibrary library,
    IMusicPlayer player,
    IDialogs dialogs
) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] string announcementText = "This is a test announcement playing over your music.";
    [ObservableProperty] double duckLevel = 0.2;
    [ObservableProperty] string duckLevelText = "20%";
    [ObservableProperty] ObservableCollection<TrackItem> tracks = [];
    [ObservableProperty] TrackItem? selectedTrack;
    [ObservableProperty] string nowPlaying = "No track selected";
    [ObservableProperty] bool needsPermission;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] bool isSpeaking;

    public async void OnAppearing()
    {
        if (Tracks.Count == 0)
            await LoadTracks();
    }

    public void OnDisappearing() { }

    partial void OnDuckLevelChanged(double value) =>
        DuckLevelText = $"{value * 100:0}%";

    async Task LoadTracks()
    {
        IsBusy = true;
        try
        {
            var status = await library.CheckPermissionAsync();
            if (status != PermissionStatus.Granted)
                status = await library.RequestPermissionAsync();

            if (status != PermissionStatus.Granted)
            {
                NeedsPermission = true;
                return;
            }

            NeedsPermission = false;
            var all = await library.GetAllTracksAsync();
            Tracks = new ObservableCollection<TrackItem>(all.Select(t => new TrackItem(t)));

            foreach (var item in Tracks)
                _ = item.LoadAlbumArt(library);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task PlayTrack(TrackItem? item)
    {
        if (item == null) return;
        try
        {
            await player.PlayAsync(item.Track);
            SelectedTrack = item;
            NowPlaying = $"▶ {item.Title} — {item.Artist}";
        }
        catch (Exception ex)
        {
            await dialogs.Alert("Playback Error", ex.Message);
        }
    }

    [RelayCommand]
    void StopMusic()
    {
        player.Stop();
        SelectedTrack = null;
        NowPlaying = "No track selected";
    }

    [RelayCommand]
    async Task SpeakAndDuck()
    {
        if (string.IsNullOrWhiteSpace(AnnouncementText))
        {
            await dialogs.Alert("Nothing to say", "Type some text to announce first.");
            return;
        }

        // Duck the currently playing music (if any), speak the announcement over top,
        // then restore full volume when the duck scope is disposed.
        IsSpeaking = true;
        try
        {
            await using (player.Duck(new DuckOptions { Level = DuckLevel }))
            {
                await TextToSpeech.Default.SpeakAsync(AnnouncementText);
            }
        }
        catch (Exception ex)
        {
            await dialogs.Alert("Speech Error", ex.Message);
        }
        finally
        {
            IsSpeaking = false;
        }
    }
}
