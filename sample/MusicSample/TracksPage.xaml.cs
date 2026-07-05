using Shiny.Maui.Controls;

namespace MusicSample;

public partial class TracksPage : ShinyContentPage
{
    public TracksPage()
    {
        InitializeComponent();
    }

    async void OnTrackSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TrackItem item && BindingContext is TracksViewModel vm)
        {
            ((CollectionView)sender!).SelectedItem = null;
            await vm.SelectTrackCommand.ExecuteAsync(item);
        }
    }
}
