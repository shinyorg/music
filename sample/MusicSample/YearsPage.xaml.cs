using Shiny.Music;

namespace MusicSample;

public partial class YearsPage : ContentPage
{
    readonly IMediaLibrary mediaLibrary;

    public YearsPage(IMediaLibrary mediaLibrary)
    {
        InitializeComponent();
        this.mediaLibrary = mediaLibrary;
    }

    async void OnLoadYears(object? sender, EventArgs e)
    {
        try
        {
            var years = await this.mediaLibrary.GetYearsAsync();
            YearList.ItemsSource = years;
            Title = $"Years ({years.Count})";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
