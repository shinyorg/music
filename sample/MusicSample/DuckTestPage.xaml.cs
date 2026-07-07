using Shiny.Maui.Controls;

namespace MusicSample;

public partial class DuckTestPage : ShinyContentPage
{
    public DuckTestPage()
    {
        InitializeComponent();
    }

    async void OnTrackSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TrackItem item && BindingContext is DuckTestViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.PlayTrackCommand.ExecuteAsync(item);
        }
    }
}
