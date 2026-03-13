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
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    async void OnDecadeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not GroupedCount<int> decade)
            return;

        DecadeList.SelectedItem = null;
        await Shell.Current.GoToAsync($"tracks?decade={decade.Value}&pageTitle={Uri.EscapeDataString($"{decade.Value}s")}");
    }
}
