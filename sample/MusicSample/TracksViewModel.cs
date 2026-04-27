using System.Collections.ObjectModel;
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
) : ObservableObject, IQueryAttributable, IPageLifecycleAware
{
    [ShellProperty] public string Title { get; set; } = "Tracks";
    [ShellProperty] public string TracksJson { get; set; } = "";

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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(Title), out var t))
            Title = t?.ToString() ?? "Tracks";

        if (query.TryGetValue(nameof(TracksJson), out var json) && json is string jsonStr)
        {
            var dtos = System.Text.Json.JsonSerializer.Deserialize(
                jsonStr, TrackDtoJsonContext.Default.ListTrackDto);

            if (dtos != null)
            {
                Tracks = dtos.Select(d => new TrackItem(new MusicMetadata(
                    d.Id, d.Title, d.Artist, d.Album, d.Genre,
                    d.Duration, d.AlbumArtUri, d.IsExplicit, d.ContentUri, d.StoreId, d.Year))).ToList();

                foreach (var item in Tracks)
                    _ = item.LoadAlbumArt(library);
            }
        }

        OnPropertyChanged(nameof(Title));
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
