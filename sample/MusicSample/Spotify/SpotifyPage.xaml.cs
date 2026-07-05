using Shiny.Spotify.Maui;

namespace MusicSample.Spotify;

public partial class SpotifyPage : ContentPage
{
    public SpotifyPage()
    {
        InitializeComponent();
    }

    async void OnSearchCompleted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
        if (BindingContext is SpotifyViewModel vm)
            await vm.SearchCommand.ExecuteAsync(null);
    }

    async void OnPlaylistSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SpotifyPlaylist item && BindingContext is SpotifyViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.SelectPlaylistCommand.ExecuteAsync(item);
        }
    }

    async void OnTrackSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SpotifyTrack item && BindingContext is SpotifyViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.PlayTrackCommand.ExecuteAsync(item);
        }
    }
}
