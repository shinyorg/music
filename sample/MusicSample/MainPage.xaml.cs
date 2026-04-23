using Shiny.Music;

namespace MusicSample;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    async void OnTrackSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TrackItem item && BindingContext is MainViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.SelectTrackCommand.ExecuteAsync(item);
        }
    }

    async void OnSearchCompleted(object? sender, EventArgs e)
    {
        SearchEntry.Unfocus();
        if (BindingContext is MainViewModel vm)
            await vm.SearchCommand.ExecuteAsync(SearchEntry.Text);
    }

    async void OnGroupSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is GroupItem item && BindingContext is MainViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.SelectGroupCommand.ExecuteAsync(item);
        }
    }
}
