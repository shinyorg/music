using Shiny.Music;

namespace MusicSample;

public partial class GenresPage : ContentPage
{
    readonly IMediaLibrary mediaLibrary;

    public GenresPage(IMediaLibrary mediaLibrary)
    {
        InitializeComponent();
        this.mediaLibrary = mediaLibrary;
    }

    async void OnLoadGenres(object? sender, EventArgs e)
    {
        try
        {
            var genres = await this.mediaLibrary.GetGenresAsync();
            GenreList.ItemsSource = genres;
            Title = $"Genres ({genres.Count})";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
