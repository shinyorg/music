namespace MusicSample;

public partial class LyricsPage : ContentPage
{
    readonly PlayerViewModel vm;

    public LyricsPage(PlayerViewModel playerViewModel)
    {
        InitializeComponent();
        this.vm = playerViewModel;
        BindingContext = playerViewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        this.vm.LyricHighlightChanged += OnLyricHighlightChanged;
        ScrollToActiveLyric();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.vm.LyricHighlightChanged -= OnLyricHighlightChanged;
    }

    void OnLyricHighlightChanged(object? sender, int index)
    {
        if (index >= 0 && index < this.vm.LyricLines.Count)
            LyricsCollection.ScrollTo(index, position: ScrollToPosition.Center, animate: true);
    }

    void ScrollToActiveLyric()
    {
        for (var i = 0; i < this.vm.LyricLines.Count; i++)
        {
            if (this.vm.LyricLines[i].LyricColor == Color.FromArgb("#512BD4"))
            {
                LyricsCollection.ScrollTo(i, position: ScrollToPosition.Center, animate: false);
                break;
            }
        }
    }

    async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
