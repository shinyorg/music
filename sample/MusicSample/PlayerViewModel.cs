using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny.Music;

namespace MusicSample;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    readonly IMusicPlayer player;
    readonly IMediaLibrary library;
    readonly ILyricsProvider lyricsProvider;
    IDispatcherTimer? positionTimer;
    IDispatcher? dispatcher;
    bool isUserDragging;
    int currentLyricIndex = -1;

    public PlayerViewModel(IMusicPlayer player, IMediaLibrary library, ILyricsProvider lyricsProvider)
    {
        this.player = player;
        this.library = library;
        this.lyricsProvider = lyricsProvider;

        this.player.StateChanged += (_, state) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PlayPauseIcon = state == PlaybackState.Playing ? "⏸" : "▶";
                UpdatePositionTimer(state);
            });
        };

        this.player.PlaybackCompleted += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(ResetPlayerUI);
        };
    }

    // ── Observable Properties ───────────────────────────────────

    [ObservableProperty] bool isSheetOpen;
    [ObservableProperty] string nowPlayingTitle = "Not Playing";
    [ObservableProperty] string nowPlayingArtist = "";
    [ObservableProperty] string nowPlayingAlbum = "";
    [ObservableProperty] ImageSource? albumArtSource;
    [ObservableProperty] string playPauseIcon = "▶";
    [ObservableProperty] string positionText = "0:00";
    [ObservableProperty] string durationText = "0:00";
    [ObservableProperty] double sliderValue;
    [ObservableProperty] double volume = 1.0;
    [ObservableProperty] bool hasLyrics;

    public ObservableCollection<LyricLineVM> LyricLines { get; } = [];

    public event EventHandler<int>? LyricHighlightChanged;

    // ── Public Methods ──────────────────────────────────────────

    public void SetDispatcher(IDispatcher disp) => this.dispatcher = disp;

    public async Task PlayTrack(MusicMetadata track)
    {
        await this.player.PlayAsync(track);
        NowPlayingTitle = track.Title ?? "Unknown";
        NowPlayingArtist = track.Artist ?? "Unknown";
        NowPlayingAlbum = track.Album ?? "";
        _ = LoadAlbumArt(track.Id);
        _ = LoadLyrics(track);
    }

    // ── Commands ────────────────────────────────────────────────

    [RelayCommand]
    void PlayPause()
    {
        switch (this.player.State)
        {
            case PlaybackState.Playing:
                this.player.Pause();
                break;
            case PlaybackState.Paused:
                this.player.Resume();
                break;
        }
    }

    [RelayCommand]
    void Stop()
    {
        this.player.Stop();
        ResetPlayerUI();
    }

    public void ApplyVolume(double value)
    {
        Volume = value;
        this.player.Volume = (float)value;
    }

    // ── Seek ────────────────────────────────────────────────────

    public void SeekDragStarted() => this.isUserDragging = true;

    public void SeekDragCompleted(double value)
    {
        this.isUserDragging = false;
        var duration = this.player.Duration;
        if (duration.TotalSeconds > 0)
            this.player.Seek(TimeSpan.FromSeconds(value * duration.TotalSeconds));
    }

    public void SeekSliderChanged(double value)
    {
        if (!this.isUserDragging) return;
        var duration = this.player.Duration;
        if (duration.TotalSeconds > 0)
            PositionText = FormatTime(TimeSpan.FromSeconds(value * duration.TotalSeconds));
    }

    // ── Album Art ───────────────────────────────────────────────

    async Task LoadAlbumArt(string trackId)
    {
        try
        {
            var path = await this.library.GetAlbumArtPathAsync(trackId);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AlbumArtSource = path != null ? ImageSource.FromFile(path) : null;
            });
        }
        catch { }
    }

    // ── Lyrics ──────────────────────────────────────────────────

    async Task LoadLyrics(MusicMetadata track)
    {
        this.currentLyricIndex = -1;
        LyricLines.Clear();
        HasLyrics = false;

        try
        {
            var result = await this.lyricsProvider.GetLyricsAsync(track);
            if (result == null) return;

            if (!string.IsNullOrWhiteSpace(result.SyncedLyrics))
            {
                foreach (var (time, text) in ParseLrc(result.SyncedLyrics))
                    LyricLines.Add(new LyricLineVM(time, text));
            }
            else if (!string.IsNullOrWhiteSpace(result.PlainLyrics))
            {
                LyricLines.Add(new LyricLineVM(TimeSpan.Zero, result.PlainLyrics));
            }

            HasLyrics = LyricLines.Count > 0;
        }
        catch { }
    }

    void UpdateLyricHighlight(TimeSpan position)
    {
        if (LyricLines.Count == 0 || (LyricLines.Count == 1 && LyricLines[0].Time == TimeSpan.Zero))
            return;

        var newIndex = -1;
        for (var i = LyricLines.Count - 1; i >= 0; i--)
        {
            if (position >= LyricLines[i].Time)
            {
                newIndex = i;
                break;
            }
        }

        if (newIndex == this.currentLyricIndex) return;

        if (this.currentLyricIndex >= 0 && this.currentLyricIndex < LyricLines.Count)
            LyricLines[this.currentLyricIndex].SetActive(false);

        if (newIndex >= 0)
            LyricLines[newIndex].SetActive(true);

        this.currentLyricIndex = newIndex;
        LyricHighlightChanged?.Invoke(this, newIndex);
    }

    static List<(TimeSpan Time, string Text)> ParseLrc(string lrc)
    {
        var lines = new List<(TimeSpan, string)>();
        var regex = LrcLineRegex();

        foreach (var line in lrc.Split('\n'))
        {
            var match = regex.Match(line);
            if (!match.Success) continue;

            var minutes = int.Parse(match.Groups[1].Value);
            var seconds = double.Parse(match.Groups[2].Value);
            var text = match.Groups[3].Value.Trim();

            if (string.IsNullOrEmpty(text)) continue;

            lines.Add((TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds), text));
        }

        return lines.OrderBy(l => l.Item1).ToList();
    }

    [GeneratedRegex(@"\[(\d+):(\d+\.?\d*)\](.*)")]
    private static partial Regex LrcLineRegex();

    // ── Position Timer ──────────────────────────────────────────

    void UpdatePositionTimer(PlaybackState state)
    {
        if (state == PlaybackState.Playing)
            StartPositionTimer();
        else
            StopPositionTimer();
    }

    void StartPositionTimer()
    {
        if (this.positionTimer != null || this.dispatcher == null) return;

        this.positionTimer = this.dispatcher.CreateTimer();
        this.positionTimer.Interval = TimeSpan.FromMilliseconds(500);
        this.positionTimer.Tick += OnPositionTick;
        this.positionTimer.Start();
    }

    void StopPositionTimer()
    {
        if (this.positionTimer == null) return;
        this.positionTimer.Stop();
        this.positionTimer.Tick -= OnPositionTick;
        this.positionTimer = null;
    }

    void OnPositionTick(object? sender, EventArgs e)
    {
        if (this.isUserDragging) return;

        var position = this.player.Position;
        var duration = this.player.Duration;

        PositionText = FormatTime(position);
        DurationText = FormatTime(duration);
        SliderValue = duration.TotalSeconds > 0
            ? position.TotalSeconds / duration.TotalSeconds
            : 0;

        UpdateLyricHighlight(position);
    }

    void ResetPlayerUI()
    {
        SliderValue = 0;
        PositionText = "0:00";
        DurationText = "0:00";
        PlayPauseIcon = "▶";
        this.currentLyricIndex = -1;
        foreach (var line in LyricLines)
            line.SetActive(false);
    }

    static string FormatTime(TimeSpan time) =>
        time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss")
            : time.ToString(@"m\:ss");

    public void Dispose() => StopPositionTimer();
}

// ── Lyric Line VM ───────────────────────────────────────────

public partial class LyricLineVM : ObservableObject
{
    public TimeSpan Time { get; }
    public string Text { get; }

    [ObservableProperty] double lyricFontSize = 15;
    [ObservableProperty] Color lyricColor = Colors.Gray;
    [ObservableProperty] FontAttributes lyricFontAttr = FontAttributes.None;

    public LyricLineVM(TimeSpan time, string text)
    {
        Time = time;
        Text = text;
    }

    public void SetActive(bool active)
    {
        LyricFontSize = active ? 17 : 15;
        LyricColor = active ? Color.FromArgb("#512BD4") : Colors.Gray;
        LyricFontAttr = active ? FontAttributes.Bold : FontAttributes.None;
    }
}
