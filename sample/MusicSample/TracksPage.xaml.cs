using Shiny.Music;

namespace MusicSample;

[QueryProperty(nameof(Genre), "genre")]
[QueryProperty(nameof(Decade), "decade")]
[QueryProperty(nameof(Year), "year")]
[QueryProperty(nameof(PlaylistId), "playlistId")]
[QueryProperty(nameof(PageTitle), "pageTitle")]
public partial class TracksPage : ContentPage
{
    readonly IMediaLibrary mediaLibrary;
    readonly IMusicPlayer musicPlayer;
    MusicMetadata? selectedTrack;
    IDispatcherTimer? positionTimer;
    bool isUserDragging;

    public string? Genre { get; set; }
    public string? Decade { get; set; }
    public string? Year { get; set; }
    public string? PlaylistId { get; set; }
    public string? PageTitle { get; set; }

    public TracksPage(IMediaLibrary mediaLibrary, IMusicPlayer musicPlayer)
    {
        InitializeComponent();
        this.mediaLibrary = mediaLibrary;
        this.musicPlayer = musicPlayer;

        this.musicPlayer.StateChanged += OnStateChanged;
        this.musicPlayer.PlaybackCompleted += OnPlaybackCompleted;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.IsNullOrEmpty(PageTitle))
            Title = Uri.UnescapeDataString(PageTitle);

        await LoadTracksAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopPositionTimer();
    }

    async Task LoadTracksAsync()
    {
        try
        {
            IReadOnlyList<MusicMetadata> tracks;

            if (!string.IsNullOrEmpty(PlaylistId))
            {
                tracks = await this.mediaLibrary.GetPlaylistTracksAsync(PlaylistId);
            }
            else
            {
                var filter = new MusicFilter();

                if (!string.IsNullOrEmpty(Genre))
                    filter = new MusicFilter { Genre = Uri.UnescapeDataString(Genre) };
                else if (!string.IsNullOrEmpty(Year))
                    filter = new MusicFilter { Year = int.Parse(Year) };
                else if (!string.IsNullOrEmpty(Decade))
                    filter = new MusicFilter { Decade = int.Parse(Decade) };

                tracks = await this.mediaLibrary.GetTracksAsync(filter);
            }

            TrackList.ItemsSource = tracks;
            Title = $"{Title} ({tracks.Count} tracks)";
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
            LblNowPlaying.Text = $"{this.selectedTrack.Title} — {this.selectedTrack.Artist}";
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

    void OnStateChanged(object? sender, PlaybackState state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LblPlaybackState.Text = state.ToString();
            if (state == PlaybackState.Playing)
                StartPositionTimer();
            else
                StopPositionTimer();
        });
    }

    void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LblNowPlaying.Text = "Finished";
            ResetSeekBar();
        });
    }

    void StartPositionTimer()
    {
        if (this.positionTimer != null)
            return;

        this.positionTimer = Dispatcher.CreateTimer();
        this.positionTimer.Interval = TimeSpan.FromMilliseconds(500);
        this.positionTimer.Tick += OnPositionTimerTick;
        this.positionTimer.Start();
    }

    void StopPositionTimer()
    {
        if (this.positionTimer == null)
            return;

        this.positionTimer.Stop();
        this.positionTimer.Tick -= OnPositionTimerTick;
        this.positionTimer = null;
    }

    void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (this.isUserDragging)
            return;

        var position = this.musicPlayer.Position;
        var duration = this.musicPlayer.Duration;

        LblPosition.Text = FormatTime(position);
        LblDuration.Text = FormatTime(duration);
        SliderPosition.Value = duration.TotalSeconds > 0
            ? position.TotalSeconds / duration.TotalSeconds
            : 0;
    }

    void OnSliderDragStarted(object? sender, EventArgs e) => this.isUserDragging = true;

    void OnSliderDragCompleted(object? sender, EventArgs e)
    {
        this.isUserDragging = false;
        var duration = this.musicPlayer.Duration;
        if (duration.TotalSeconds > 0)
        {
            var seekTo = TimeSpan.FromSeconds(SliderPosition.Value * duration.TotalSeconds);
            this.musicPlayer.Seek(seekTo);
        }
    }

    void OnSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (!this.isUserDragging)
            return;

        var duration = this.musicPlayer.Duration;
        if (duration.TotalSeconds > 0)
        {
            var preview = TimeSpan.FromSeconds(e.NewValue * duration.TotalSeconds);
            LblPosition.Text = FormatTime(preview);
        }
    }

    void ResetSeekBar()
    {
        SliderPosition.Value = 0;
        LblPosition.Text = "0:00";
        LblDuration.Text = "0:00";
    }

    static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");
}
