using Shiny.Music;

namespace MusicSample;

public partial class MainPage : ContentPage
{
    readonly IMediaLibrary mediaLibrary;
    readonly IMusicPlayer musicPlayer;
    MusicMetadata? selectedTrack;

    public MainPage(IMediaLibrary mediaLibrary, IMusicPlayer musicPlayer)
    {
        InitializeComponent();
        this.mediaLibrary = mediaLibrary;
        this.musicPlayer = musicPlayer;

        this.musicPlayer.StateChanged += (_, state) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblPlaybackState.Text = state.ToString();
            });
        };

        this.musicPlayer.PlaybackCompleted += (_, _) =>
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
            var status = await this.mediaLibrary.RequestPermissionAsync();
            LblPermissionStatus.Text = status.ToString();

            if (status == Shiny.Music.PermissionStatus.Granted)
                await this.LoadTracks();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    async void OnLoadAll(object? sender, EventArgs e)
    {
        await this.LoadTracks();
    }

    async void OnSearch(object? sender, EventArgs e)
    {
        var query = EntrySearch.Text?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            await this.LoadTracks();
            return;
        }

        try
        {
            var tracks = await this.mediaLibrary.SearchTracksAsync(query);
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
        this.selectedTrack = e.CurrentSelection.FirstOrDefault() as MusicMetadata;
        if (this.selectedTrack != null)
            LblNowPlaying.Text = $"{this.selectedTrack.Title} - {this.selectedTrack.Artist}";
    }

    async void OnPlay(object? sender, EventArgs e)
    {
        if (this.selectedTrack == null)
        {
            await DisplayAlertAsync("Info", "Select a track first", "OK");
            return;
        }

        try
        {
            await this.musicPlayer.PlayAsync(this.selectedTrack);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Playback Error", ex.Message, "OK");
        }
    }

    void OnPause(object? sender, EventArgs e) => this.musicPlayer.Pause();
    void OnStop(object? sender, EventArgs e) => this.musicPlayer.Stop();

    async void OnInfo(object? sender, EventArgs e)
    {
        if (this.selectedTrack == null)
        {
            await DisplayAlertAsync("Info", "Select a track first", "OK");
            return;
        }

        await Navigation.PushAsync(new TrackDetailPage(this.selectedTrack));
    }

    async void OnCopy(object? sender, EventArgs e)
    {
        if (this.selectedTrack == null)
        {
            await DisplayAlertAsync("Info", "Select a track first", "OK");
            return;
        }

        try
        {
            var destDir = Path.Combine(FileSystem.AppDataDirectory, "CopiedMusic");
            var safeTitle = string.Join("_", (this.selectedTrack.Title ?? "track").Split(Path.GetInvalidFileNameChars()));
            var destPath = Path.Combine(destDir, $"{safeTitle}.m4a");

            var success = await this.mediaLibrary.CopyTrackAsync(this.selectedTrack, destPath);
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
            var tracks = await this.mediaLibrary.GetAllTracksAsync();
            TrackList.ItemsSource = tracks;
            Title = $"Music Library ({tracks.Count} tracks)";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
