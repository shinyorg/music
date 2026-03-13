using Shiny.Music;

namespace MusicSample;

public partial class DecadesPage : ContentPage
{
    readonly IMediaLibrary mediaLibrary;

    public DecadesPage(IMediaLibrary mediaLibrary)
    {
        InitializeComponent();
        this.mediaLibrary = mediaLibrary;
    }

    async void OnLoadDecades(object? sender, EventArgs e)
    {
        try
        {
            var decades = await this.mediaLibrary.GetDecadesAsync();
            DecadeList.ItemsSource = decades;
            Title = $"Decades ({decades.Count})";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
