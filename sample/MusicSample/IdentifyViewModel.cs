using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;
using Shiny.Music;

namespace MusicSample;

[ShellMap<IdentifyPage>("Identify")]
public partial class IdentifyViewModel(
    IMusicPlayer player,
    IMusicIdentifier identifier
) : ObservableObject
{
    CancellationTokenSource? cts;

    [ObservableProperty] bool isListening;
    [ObservableProperty] bool hasResult;
    [ObservableProperty] string? title;
    [ObservableProperty] string? artist;
    [ObservableProperty] string? album;
    [ObservableProperty] string? genre;
    [ObservableProperty] ImageSource? artworkSource;
    [ObservableProperty] string? musicUrl;
    [ObservableProperty] string? statusText;
    [ObservableProperty] bool isPlayingPreview;

    [RelayCommand]
    async Task Listen()
    {
        if (IsListening)
        {
            Cancel();
            return;
        }

        Reset();
        IsListening = true;
        StatusText = "Listening...";
        cts = new CancellationTokenSource();

        try
        {
            // Stop any currently playing music so the mic picks up external audio
            if (player.State != PlaybackState.Stopped)
                player.Stop();

            var result = await identifier.ListenAsync(cts.Token);
            if (result == null)
            {
                StatusText = "No match found. Try again.";
                return;
            }

            Title = result.Title;
            Artist = result.Artist;
            Album = result.Album;
            Genre = result.Genre;
            MusicUrl = result.MusicUrl;
            ArtworkSource = !string.IsNullOrEmpty(result.ArtworkUrl)
                ? ImageSource.FromUri(new Uri(result.ArtworkUrl))
                : null;
            HasResult = true;
            StatusText = null;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsListening = false;
            cts?.Dispose();
            cts = null;
        }
    }

    [RelayCommand]
    async Task OpenMusicUrl()
    {
        if (!string.IsNullOrEmpty(MusicUrl))
            await Launcher.Default.OpenAsync(new Uri(MusicUrl));
    }

    void Cancel()
    {
        cts?.Cancel();
    }

    void Reset()
    {
        HasResult = false;
        Title = null;
        Artist = null;
        Album = null;
        Genre = null;
        ArtworkSource = null;
        MusicUrl = null;
        StatusText = null;
    }
}
