using Microsoft.Maui.Controls.Shapes;
using Shiny.Maui.Controls;

namespace MusicSample;

public partial class PlayerSheet : ContentView
{
    public PlayerSheet()
    {
        InitializeComponent();

        Sheet.Detents.Clear();
        Sheet.Detents.Add(new DetentValue(0.50));
        Sheet.Detents.Add(new DetentValue(1.0));

        Sheet.HeaderTemplate = BuildMiniPlayer();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        Sheet.BindingContext = BindingContext;
    }

    View BuildMiniPlayer()
    {
        var albumArt = new Image
        {
            WidthRequest = 40,
            HeightRequest = 40,
            Aspect = Aspect.AspectFill,
            Clip = new RoundRectangleGeometry(new CornerRadius(6), new Rect(0, 0, 40, 40))
        };
        albumArt.SetBinding(Image.SourceProperty, nameof(PlayerViewModel.AlbumArtSource));

        var titleLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        titleLabel.SetBinding(Label.TextProperty, nameof(PlayerViewModel.NowPlayingTitle));

        var artistLabel = new Label
        {
            FontSize = 12,
            TextColor = Colors.Gray,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        artistLabel.SetBinding(Label.TextProperty, nameof(PlayerViewModel.NowPlayingArtist));

        var infoStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children = { titleLabel, artistLabel }
        };

        var playBtn = new Button
        {
            FontSize = 20,
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 20,
            Padding = 0,
            BackgroundColor = Colors.Transparent
        };
        playBtn.SetBinding(Button.TextProperty, nameof(PlayerViewModel.PlayPauseIcon));
        playBtn.Clicked += (_, _) =>
        {
            if (BindingContext is PlayerViewModel vm)
                vm.PlayPauseCommand.Execute(null);
        };

        var grid = new Grid
        {
            Padding = new Thickness(16, 10),
            ColumnDefinitions =
            {
                new ColumnDefinition(40),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            HeightRequest = 56,
            BackgroundColor = Color.FromArgb("#F2F2F7")
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (_, _) =>
        {
            if (BindingContext is PlayerViewModel vm)
                vm.IsSheetOpen = true;
        };
        grid.GestureRecognizers.Add(tapGesture);

        grid.Add(albumArt, 0);
        grid.Add(infoStack, 1);
        grid.Add(playBtn, 2);

        return grid;
    }

    void OnDragStarted(object? sender, EventArgs e)
    {
        if (BindingContext is PlayerViewModel vm)
            vm.SeekDragStarted();
    }

    void OnDragCompleted(object? sender, EventArgs e)
    {
        if (BindingContext is PlayerViewModel vm && sender is Slider s)
            vm.SeekDragCompleted(s.Value);
    }

    void OnSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (BindingContext is PlayerViewModel vm)
            vm.SeekSliderChanged(e.NewValue);
    }

    void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        if (BindingContext is PlayerViewModel vm)
            vm.ApplyVolume(e.NewValue);
    }

    async void OnLyricsClicked(object? sender, EventArgs e)
    {
        if (BindingContext is PlayerViewModel vm)
        {
            var page = new LyricsPage(vm);
            await Navigation.PushModalAsync(page);
        }
    }
}
