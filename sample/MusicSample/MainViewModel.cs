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
    [ObservableProperty] bool isPlaylistPickerOpen;
    [ObservableProperty] string newPlaylistName = "";
    [ObservableProperty] ObservableCollection<PlaylistPickerItem> customPlaylists = [];

    MusicMetadata? pendingPlaylistTrack;

    // Cancels the in-flight category load (its result handling) and the album-art lazy
    // loading it kicked off, so switching categories abandons the previous one's work.
    CancellationTokenSource? loadCts;

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
            SelectCategory(result);
    }

    void SelectCategory(string category)
    {
        // Change the wording immediately, then kick off the load WITHOUT awaiting it so this
        // command completes right away and the picker can be reopened (which supersedes/cancels
        // the in-flight load). Awaiting here kept the command "running" for the whole slow load,
        // which blocked the picker from opening again.
        SelectedCategory = category;
        _ = LoadCategory();
    }

    [RelayCommand]
    async Task Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await LoadCategory();
            return;
        }

        // Cancel any in-flight category load / album-art loading before searching.
        loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        loadCts = cts;
        var ct = cts.Token;

        IsBusy = true;
        try
        {
            var results = await library.SearchTracksAsync(query);
            if (ct.IsCancellationRequested) return;
            SetTracks(results, ct);
            ShowGroups = false;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
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

        await navigator.NavigateTo<TracksViewModel>(vm => vm.SetTracks(title, tracks));
    }

    [RelayCommand]
    Task OpenNavigation() => navigator.NavigateTo("Navigate");

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

        var created = await library.CreatePlaylistAsync(name);
        await library.AddTrackToPlaylistAsync(created.Id, pendingPlaylistTrack);
        IsPlaylistPickerOpen = false;

        if (SelectedCategory == "Custom Playlists")
            await LoadCategory();
    }

    [RelayCommand]
    async Task AddToExistingPlaylist(PlaylistPickerItem? playlist)
    {
        if (playlist == null || pendingPlaylistTrack == null) return;
        await library.AddTrackToPlaylistAsync(playlist.Id, pendingPlaylistTrack);
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

        // Cancel the previous category's load (its result handling) and any album-art
        // lazy loading it started, then begin a fresh one.
        loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        loadCts = cts;
        var ct = cts.Token;

        IsBusy = true;

        // Let the swapped category label + spinner paint before the retrieval starts.
        await Task.Yield();

        try
        {
            switch (SelectedCategory)
            {
                case "Library":
                    var allTracks = await library.GetAllTracksAsync();
                    if (ct.IsCancellationRequested) return;
                    SetTracks(allTracks, ct);
                    ShowGroups = false;
                    break;

                case "Playlists":
                    var playlists = await library.GetPlaylistsAsync();
                    if (ct.IsCancellationRequested) return;
                    Groups = new ObservableCollection<GroupItem>(
                        playlists.Select(p => new GroupItem(p.Id, p.Name, p.SongCount)));
                    ShowGroups = true;
                    break;

                case "Genres":
                    var genres = await library.GetGenresAsync();
                    if (ct.IsCancellationRequested) return;
                    Groups = new ObservableCollection<GroupItem>(
                        genres.Select(g => new GroupItem(g.Value, g.Value, g.Count)));
                    ShowGroups = true;
                    break;

                case "Decades":
                    var decades = await library.GetDecadesAsync();
                    if (ct.IsCancellationRequested) return;
                    Groups = new ObservableCollection<GroupItem>(
                        decades.Select(d => new GroupItem(d.Value.ToString(), $"{d.Value}s", d.Count)));
                    ShowGroups = true;
                    break;

                case "Years":
                    var years = await library.GetYearsAsync();
                    if (ct.IsCancellationRequested) return;
                    Groups = new ObservableCollection<GroupItem>(
                        years.Select(y => new GroupItem(y.Value.ToString(), y.Value.ToString(), y.Count)));
                    ShowGroups = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
                await dialogs.Alert("Load Error", ex.Message);
        }
        finally
        {
            // Only the still-current load clears the spinner; a superseded load leaves it
            // to the newer one that cancelled it.
            if (!ct.IsCancellationRequested)
                IsBusy = false;
        }
    }

    void SetTracks(IReadOnlyList<MusicMetadata> rawTracks, CancellationToken cancellationToken)
    {
        var items = rawTracks.Select(t => new TrackItem(t)).ToList();
        Tracks = new ObservableCollection<TrackItem>(items);

        // Load album art in the background; cancels when the category is switched away.
        foreach (var item in items)
            _ = item.LoadAlbumArt(library, cancellationToken);
    }

    async Task LoadCustomPlaylists()
    {
        var playlists = await library.GetPlaylistsAsync();
        CustomPlaylists = new ObservableCollection<PlaylistPickerItem>(
            playlists.Select(p => new PlaylistPickerItem(p.Id, p.Name)));
    }


}

public record GroupItem(string Id, string DisplayName, int Count);
public record PlaylistPickerItem(string Id, string Name);
