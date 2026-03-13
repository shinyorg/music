using Shiny.Music;

namespace MusicSample;

public partial class PlaylistsPage : ContentPage
{
    readonly IMediaLibrary mediaLibrary;

    public PlaylistsPage(IMediaLibrary mediaLibrary)
    {
        InitializeComponent();
        this.mediaLibrary = mediaLibrary;
    }

    async void OnLoadPlaylists(object? sender, EventArgs e)
    {
        try
        {
            var playlists = await this.mediaLibrary.GetPlaylistsAsync();
            PlaylistList.ItemsSource = playlists;
            Title = $"Playlists ({playlists.Count})";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    async void OnPlaylistSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PlaylistInfo playlist)
            return;

        PlaylistList.SelectedItem = null;
        await Shell.Current.GoToAsync($"tracks?playlistId={Uri.EscapeDataString(playlist.Id)}&pageTitle={Uri.EscapeDataString(playlist.Name)}");
    }
}
