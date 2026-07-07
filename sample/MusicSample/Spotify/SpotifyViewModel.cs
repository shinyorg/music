using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;
using Shiny.Spotify.Maui;

namespace MusicSample.Spotify;

[ShellMap<SpotifyPage>(registerRoute: false)]
public partial class SpotifyViewModel(
    ISpotifyClient client,
    ISpotifyRemote remote,
    IDialogs dialogs
) : ObservableObject, IPageLifecycleAware
{
    [ObservableProperty] bool isConfigured = SpotifyConfig.IsConfigured;
    [ObservableProperty] bool isConnected;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string mode = "Playlists"; // "Playlists" | "Search"
    [ObservableProperty] string searchQuery = "";
    [ObservableProperty] string? nowPlaying;
    [ObservableProperty] bool isPaused = true;

    public ObservableCollection<SpotifyPlaylist> Playlists { get; } = [];
    public ObservableCollection<SpotifyTrack> Tracks { get; } = [];

    public bool ShowPlaylists => Mode == "Playlists";
    public bool HasNowPlaying => !string.IsNullOrEmpty(NowPlaying);

    static readonly Color TabActive = Color.FromArgb("#1DB954");
    static readonly Color TabInactive = Color.FromArgb("#22808080");
    static readonly Color TabMuted = Color.FromArgb("#808080");

    public Color PlaylistTabBackground => ShowPlaylists ? TabActive : TabInactive;
    public Color PlaylistTabText => ShowPlaylists ? Colors.White : TabMuted;
    public Color SearchTabBackground => ShowPlaylists ? TabInactive : TabActive;
    public Color SearchTabText => ShowPlaylists ? TabMuted : Colors.White;

    partial void OnModeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowPlaylists));
        OnPropertyChanged(nameof(PlaylistTabBackground));
        OnPropertyChanged(nameof(PlaylistTabText));
        OnPropertyChanged(nameof(SearchTabBackground));
        OnPropertyChanged(nameof(SearchTabText));
    }

    partial void OnNowPlayingChanged(string? value) => OnPropertyChanged(nameof(HasNowPlaying));

    public async void OnAppearing()
    {
        remote.PlayerStateChanged += OnRemotePlayerStateChanged;

        if (!IsConfigured)
            return;

        await client.RestoreAsync();
        IsConnected = client.IsAuthenticated;
        if (IsConnected && Playlists.Count == 0)
            await LoadPlaylists();
    }

    public void OnDisappearing()
    {
        remote.PlayerStateChanged -= OnRemotePlayerStateChanged;
    }

    void OnRemotePlayerStateChanged(object? sender, RemotePlayerState state)
    {
        // App Remote raises this on a native binder thread - marshal to the UI.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsPaused = state.IsPaused;
            NowPlaying = string.IsNullOrEmpty(state.TrackName)
                ? null
                : $"{state.TrackName} — {state.ArtistName}";
        });
    }

    [RelayCommand]
    async Task Connect()
    {
        if (!IsConfigured)
        {
            await dialogs.Alert(
                "Setup Required",
                "Add your Spotify Client ID in SpotifyConfig.cs and register the redirect URI in the Spotify dashboard.");
            return;
        }

        await Run(async () =>
        {
            await client.LoginAsync();
            IsConnected = client.IsAuthenticated;
            if (IsConnected)
                await LoadPlaylists();
        });
    }

    [RelayCommand]
    async Task Disconnect()
    {
        remote.Disconnect();
        await client.LogoutAsync();
        IsConnected = false;
        Playlists.Clear();
        Tracks.Clear();
        NowPlaying = null;
    }

    [RelayCommand]
    void SetMode(string value) => Mode = value;

    [RelayCommand]
    async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
            return;

        Mode = "Search";
        await Run(async () =>
        {
            var results = await client.SearchTracksAsync(SearchQuery);
            ReplaceTracks(results);
        });
    }

    [RelayCommand]
    async Task SelectPlaylist(SpotifyPlaylist? playlist)
    {
        if (playlist == null)
            return;

        Mode = "Search"; // reuse the track list surface
        await Run(async () =>
        {
            var tracks = await client.GetPlaylistTracksAsync(playlist.Id);
            ReplaceTracks(tracks);
        });
    }

    [RelayCommand]
    async Task PlayTrack(SpotifyTrack? track)
    {
        if (track == null)
            return;

        await Run(async () =>
        {
            if (!remote.IsConnected)
                await remote.ConnectAsync(); // launches the Spotify app + authorizes

            await remote.PlayAsync(track.Uri);
            NowPlaying = $"{track.Title} — {track.Artist}";
            IsPaused = false;
        });
    }

    [RelayCommand]
    Task Pause() => Run(remote.PauseAsync);

    [RelayCommand]
    Task Resume() => Run(remote.ResumeAsync);

    [RelayCommand]
    Task Next() => Run(remote.SkipNextAsync);

    [RelayCommand]
    Task Previous() => Run(remote.SkipPreviousAsync);

    [RelayCommand]
    async Task RefreshPlaylists() => await LoadPlaylists();

    // ── Private ─────────────────────────────────────────────────────────────

    async Task LoadPlaylists()
    {
        await Run(async () =>
        {
            var playlists = await client.GetPlaylistsAsync();
            Playlists.Clear();
            foreach (var p in playlists)
                Playlists.Add(p);
        });
    }

    void ReplaceTracks(IReadOnlyList<SpotifyTrack> tracks)
    {
        Tracks.Clear();
        foreach (var t in tracks)
            Tracks.Add(t);
    }

    async Task Run(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await action();
        }
        catch (TaskCanceledException)
        {
            // user dismissed the login sheet - ignore
        }
        catch (SpotifyApiException ex) when (ex.StatusCode == 401)
        {
            IsConnected = false;
            await dialogs.Alert("Spotify", ex.Message);
        }
        catch (Exception ex)
        {
            await dialogs.Alert("Spotify", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
