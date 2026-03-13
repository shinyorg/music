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
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    async void OnYearSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not GroupedCount<int> year)
            return;

        YearList.SelectedItem = null;
        await Shell.Current.GoToAsync($"tracks?year={year.Value}&pageTitle={year.Value}");
    }
}
