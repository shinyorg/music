using Shiny.Maui.Controls.FloatingPanel;
using Shiny.Music;

namespace MusicSample;

public partial class MainPage : ShinyContentPage
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

    async void OnAddToPlaylistTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Label label
            && label.BindingContext is TrackItem item
            && BindingContext is MainViewModel vm)
        {
            await vm.OpenPlaylistPickerCommand.ExecuteAsync(item);
        }
    }

    void OnPlaylistPickerBackdropTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is MainViewModel vm)
            vm.IsPlaylistPickerOpen = false;
    }

    async void OnPlaylistPickerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PlaylistPickerItem item && BindingContext is MainViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.AddToExistingPlaylistCommand.ExecuteAsync(item);
        }
    }
}
