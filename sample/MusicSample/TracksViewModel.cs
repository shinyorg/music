using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;
using Shiny.Music;

namespace MusicSample;

[ShellMap<TracksPage>("Tracks")]
public partial class TracksViewModel(
    INavigator navigator,
    IDialogs dialogs,
    IMediaLibrary library,
    PlayerViewModel player
) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] string title = "Tracks";

    public PlayerViewModel Player => player;

    public List<TrackItem> Tracks
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public void SetTracks(string title, IReadOnlyList<MusicMetadata> tracks)
    {
        Title = title;
        Tracks = tracks.Select(t => new TrackItem(t)).ToList();

        foreach (var item in Tracks)
            _ = item.LoadAlbumArt(library);
    }

    public void OnAppearing()
    {
        player.SetDispatcher(Application.Current!.Dispatcher);
        player.OnAppearing();
    }

    public void OnDisappearing()
    {
        player.OnDisappearing();
    }

    [RelayCommand]
    async Task SelectTrack(TrackItem? item)
    {
        if (item == null) return;
        try
        {
            await player.PlayTrack(item.Track);
        }
        catch (Exception ex)
        {
            await dialogs.Alert("Playback Error", ex.Message);
        }
    }

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
