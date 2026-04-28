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
    IMusicManager musicManager,
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
    [ObservableProperty] bool isPlaylistPickerOpen;
    [ObservableProperty] string newPlaylistName = "";
    [ObservableProperty] ObservableCollection<PlaylistPickerItem> customPlaylists = [];

    MusicMetadata? pendingPlaylistTrack;

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
            "Library", "Playlists", "Custom Playlists", "Genres", "Decades", "Years"
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
            case "Custom Playlists":
                tracks = await musicManager.GetPlaylistTracks(item.Id);
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

        await navigator.NavigateTo<TracksViewModel>(vm => vm.SetTracks(title, tracks));
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

    // ── Playlist Picker ──────────────────────────────────────────

    [RelayCommand]
    async Task OpenPlaylistPicker(TrackItem? item)
    {
        if (item == null) return;
        pendingPlaylistTrack = item.Track;
        NewPlaylistName = "";
        await LoadCustomPlaylists();
        IsPlaylistPickerOpen = true;
    }

    [RelayCommand]
    async Task CreateAndAddToPlaylist()
    {
        var name = NewPlaylistName?.Trim();
        if (string.IsNullOrEmpty(name) || pendingPlaylistTrack == null) return;

        var id = Guid.NewGuid().ToString();
        await musicManager.CreatePlaylist(id, name);
        await musicManager.AddTrackToPlaylist(id, pendingPlaylistTrack);
        IsPlaylistPickerOpen = false;

        if (SelectedCategory == "Custom Playlists")
            await LoadCategory();
    }

    [RelayCommand]
    async Task AddToExistingPlaylist(PlaylistPickerItem? playlist)
    {
        if (playlist == null || pendingPlaylistTrack == null) return;
        await musicManager.AddTrackToPlaylist(playlist.Id, pendingPlaylistTrack);
        IsPlaylistPickerOpen = false;
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

            case "Custom Playlists":
                var custom = await musicManager.GetAllPlaylists();
                Groups = new ObservableCollection<GroupItem>(
                    custom.Select(cp => new GroupItem(cp.Id, cp.Name, cp.SongCount)));
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

    async Task LoadCustomPlaylists()
    {
        var playlists = await musicManager.GetAllPlaylists();
        CustomPlaylists = new ObservableCollection<PlaylistPickerItem>(
            playlists.Select(p => new PlaylistPickerItem(p.Id, p.Name)));
    }


}

public record GroupItem(string Id, string DisplayName, int Count);
public record PlaylistPickerItem(string Id, string Name);
