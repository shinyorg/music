using Shiny.Music;

namespace MusicSample;

public partial class MainPage : ContentPage
{
    readonly IMediaLibrary _mediaLibrary;
    readonly IMusicPlayer _musicPlayer;
    MusicMetadata? _selectedTrack;

    public MainPage(IMediaLibrary mediaLibrary, IMusicPlayer musicPlayer)
    {
        InitializeComponent();
        _mediaLibrary = mediaLibrary;
        _musicPlayer = musicPlayer;

        _musicPlayer.StateChanged += (_, state) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblPlaybackState.Text = state.ToString();
            });
        };

        _musicPlayer.PlaybackCompleted += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblNowPlaying.Text = "Finished";
            });
        };
    }

    async void OnRequestPermission(object? sender, EventArgs e)
    {
        try
        {
            var status = await _mediaLibrary.RequestPermissionAsync();
            LblPermissionStatus.Text = status.ToString();

            if (status == Shiny.Music.PermissionStatus.Granted)
                await LoadTracks();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    async void OnLoadAll(object? sender, EventArgs e)
    {
        await LoadTracks();
    }

    async void OnSearch(object? sender, EventArgs e)
    {
        var query = EntrySearch.Text?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            await LoadTracks();
            return;
        }

        try
        {
            var tracks = await _mediaLibrary.SearchTracksAsync(query);
            TrackList.ItemsSource = tracks;
            Title = $"Music Library ({tracks.Count} results)";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    void OnTrackSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedTrack = e.CurrentSelection.FirstOrDefault() as MusicMetadata;
        if (_selectedTrack != null)
            LblNowPlaying.Text = $"{_selectedTrack.Title} - {_selectedTrack.Artist}";
    }

    async void OnPlay(object? sender, EventArgs e)
    {
        if (_selectedTrack == null)
        {
            await DisplayAlertAsync("Info", "Select a track first", "OK");
            return;
        }

        try
        {
            await _musicPlayer.PlayAsync(_selectedTrack);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Playback Error", ex.Message, "OK");
        }
    }

    void OnPause(object? sender, EventArgs e) => _musicPlayer.Pause();
    void OnStop(object? sender, EventArgs e) => _musicPlayer.Stop();

    async void OnCopy(object? sender, EventArgs e)
    {
        if (_selectedTrack == null)
        {
            await DisplayAlertAsync("Info", "Select a track first", "OK");
            return;
        }

        try
        {
            var destDir = Path.Combine(FileSystem.AppDataDirectory, "CopiedMusic");
            var safeTitle = string.Join("_", _selectedTrack.Title.Split(Path.GetInvalidFileNameChars()));
            var destPath = Path.Combine(destDir, $"{safeTitle}.m4a");

            var success = await _mediaLibrary.CopyTrackAsync(_selectedTrack, destPath);
            if (success)
                await DisplayAlertAsync("Success", $"Copied to:\n{destPath}", "OK");
            else
                await DisplayAlertAsync("Failed", "Could not copy this track. It may be DRM-protected.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    async Task LoadTracks()
    {
        try
        {
            var tracks = await _mediaLibrary.GetAllTracksAsync();
            TrackList.ItemsSource = tracks;
            Title = $"Music Library ({tracks.Count} tracks)";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
