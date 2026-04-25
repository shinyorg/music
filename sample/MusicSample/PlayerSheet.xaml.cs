using Microsoft.Maui.Controls.Shapes;
using Shiny.Maui.Controls;
using Shiny.Maui.Controls.FloatingPanel;

namespace MusicSample;

public partial class PlayerSheet : FloatingPanel
{
    public PlayerSheet()
    {
        InitializeComponent();

        Detents.Clear();
        Detents.Add(new DetentValue(0.50));
        Detents.Add(new DetentValue(1.0));

        HeaderTemplate = BuildMiniPlayer();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
    }

    View BuildMiniPlayer()
    {
        var albumArt = new Image
        {
            WidthRequest = 42,
            HeightRequest = 42,
            Aspect = Aspect.AspectFill,
            Clip = new RoundRectangleGeometry(new CornerRadius(8), new Rect(0, 0, 42, 42))
        };
        albumArt.SetBinding(Image.SourceProperty, nameof(PlayerViewModel.AlbumArtSource));

        var albumArtBorder = new Border
        {
            StrokeThickness = 0,
            WidthRequest = 42,
            HeightRequest = 42,
            Content = albumArt,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 2), Radius = 4, Opacity = 0.2f }
        };

        var titleLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        titleLabel.SetBinding(Label.TextProperty, nameof(PlayerViewModel.NowPlayingTitle));

        var artistLabel = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#ACACAC"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        artistLabel.SetBinding(Label.TextProperty, nameof(PlayerViewModel.NowPlayingArtist));

        var infoStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 2,
            Children = { titleLabel, artistLabel }
        };

        var playBtn = new Button
        {
            FontSize = 22,
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 20,
            Padding = 0,
            BackgroundColor = Colors.Transparent
        };
        playBtn.SetBinding(Button.TextProperty, nameof(PlayerViewModel.PlayPauseIcon));
        playBtn.TextColor = Color.FromArgb("#A29BFE");
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
                new ColumnDefinition(42),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            HeightRequest = 62
        };
        grid.SetAppThemeColor(Grid.BackgroundColorProperty,
            Color.FromArgb("#1E1E2E"), Color.FromArgb("#1A1A2C"));

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (_, _) =>
        {
            if (BindingContext is PlayerViewModel vm)
                vm.IsSheetOpen = true;
        };
        grid.GestureRecognizers.Add(tapGesture);

        grid.Add(albumArtBorder, 0);
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
