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
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
