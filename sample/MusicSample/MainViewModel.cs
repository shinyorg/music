using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;
using Shiny.Music;
using PermissionStatus = Shiny.Music.PermissionStatus;

namespace MusicSample;

[ShellMap<MainPage>(registerRoute: false)]
public partial class MainViewModel(
    IMediaLibrary library,
    INavigator navigator,
    IDialogs dialogs,
    PlayerViewModel player
) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] bool needsPermission = true;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string selectedCategory = "Library";
    [ObservableProperty] ObservableCollection<GroupItem> groups = [];
    [ObservableProperty] bool showGroups;

    public PlayerViewModel Player => player;

    public async void OnAppearing()
    {
        player.SetDispatcher(Application.Current!.Dispatcher);
        player.OnAppearing();

        var status = await library.CheckPermissionAsync();
        if (status == PermissionStatus.Granted)
        {
            NeedsPermission = false;
            await LoadLibrary();
        }
    }

    public void OnDisappearing()
    {
        player.OnDisappearing();
    }

    [RelayCommand]
    async Task RequestPermission()
    {
        var status = await library.RequestPermissionAsync();
        if (status == PermissionStatus.Granted)
        {
            NeedsPermission = false;
            await LoadLibrary();
        }
        else
        {
            await dialogs.Alert("Permission", $"Status: {status}");
        }
    }

    [RelayCommand]
    async Task ShowCategoryPicker()
    {
        var result = await dialogs.ActionSheet(
            "Select Category",
            "Cancel",
            null,
            "Library", "Playlists", "Genres", "Decades", "Years"
        );
        if (result != "Cancel")
            await SelectCategory(result);
    }

    [RelayCommand]
    async Task SelectCategory(string category)
    {
        SelectedCategory = category;
        await LoadCategory();
    }

    [RelayCommand]
    async Task Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await LoadCategory();
            return;
        }

        IsBusy = true;
        try
        {
            var results = await library.SearchTracksAsync(query);
            await SetTracks(results);
            ShowGroups = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    async Task SelectGroup(GroupItem? item)
    {
        if (item == null) return;

        IReadOnlyList<MusicMetadata> tracks;
        var title = item.DisplayName;

        switch (SelectedCategory)
        {
            case "Playlists":
                tracks = await library.GetPlaylistTracksAsync(item.Id);
                break;
            case "Genres":
                tracks = await library.GetTracksAsync(new MusicFilter { Genre = item.Id });
                break;
            case "Decades":
                tracks = await library.GetTracksAsync(new MusicFilter { Decade = int.Parse(item.Id) });
                break;
            case "Years":
                tracks = await library.GetTracksAsync(new MusicFilter { Year = int.Parse(item.Id) });
                break;
            default:
                return;
        }

        await navigator.NavigateToTracks(
            title: title,
            tracksJson: SerializeTracks(tracks)
        );
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

    // ── Private ─────────────────────────────────────────────────

    ObservableCollection<TrackItem> tracks = [];
    public ObservableCollection<TrackItem> Tracks
    {
        get => tracks;
        set => SetProperty(ref tracks, value);
    }

    async Task LoadLibrary()
    {
        SelectedCategory = "Library";
        await LoadCategory();
    }

    async Task LoadCategory()
    {
        if (NeedsPermission) return;

        IsBusy = true;
        try
        {
        switch (SelectedCategory)
        {
            case "Library":
                var allTracks = await library.GetAllTracksAsync();
                await SetTracks(allTracks);
                ShowGroups = false;
                break;

            case "Playlists":
                var playlists = await library.GetPlaylistsAsync();
                Groups = new ObservableCollection<GroupItem>(
                    playlists.Select(p => new GroupItem(p.Id, p.Name, p.SongCount)));
                ShowGroups = true;
                break;

            case "Genres":
                var genres = await library.GetGenresAsync();
                Groups = new ObservableCollection<GroupItem>(
                    genres.Select(g => new GroupItem(g.Value, g.Value, g.Count)));
                ShowGroups = true;
                break;

            case "Decades":
                var decades = await library.GetDecadesAsync();
                Groups = new ObservableCollection<GroupItem>(
                    decades.Select(d => new GroupItem(d.Value.ToString(), $"{d.Value}s", d.Count)));
                ShowGroups = true;
                break;

            case "Years":
                var years = await library.GetYearsAsync();
                Groups = new ObservableCollection<GroupItem>(
                    years.Select(y => new GroupItem(y.Value.ToString(), y.Value.ToString(), y.Count)));
                ShowGroups = true;
                break;
        }
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task SetTracks(IReadOnlyList<MusicMetadata> rawTracks)
    {
        var items = rawTracks.Select(t => new TrackItem(t)).ToList();
        Tracks = new ObservableCollection<TrackItem>(items);

        // Load album art in the background
        foreach (var item in items)
            _ = item.LoadAlbumArt(library);
    }

    // Simple serialization to pass track data to TracksPage
    static string SerializeTracks(IReadOnlyList<MusicMetadata> tracks)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            tracks.Select(t => new TrackDto(t.Id, t.Title, t.Artist, t.Album, t.Genre,
                t.Duration, t.AlbumArtUri, t.IsExplicit, t.ContentUri, t.StoreId, t.Year)).ToList(),
            TrackDtoJsonContext.Default.ListTrackDto
        );
    }
}

public record GroupItem(string Id, string DisplayName, int Count);
