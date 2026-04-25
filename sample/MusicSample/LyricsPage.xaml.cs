namespace MusicSample;

public partial class LyricsPage : ContentPage
{
    readonly PlayerViewModel vm;
    bool isInitialSync;

    public LyricsPage(PlayerViewModel playerViewModel)
    {
        InitializeComponent();
        this.vm = playerViewModel;
        BindingContext = playerViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        this.vm.LyricHighlightChanged += OnLyricHighlightChanged;

        // Defer so the CollectionView is laid out before we scroll
        this.isInitialSync = true;
        await Task.Delay(100);
        this.vm.ResyncLyricHighlight();
        this.isInitialSync = false;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.vm.LyricHighlightChanged -= OnLyricHighlightChanged;
    }

    void OnLyricHighlightChanged(object? sender, int index)
    {
        if (index >= 0 && index < this.vm.LyricLines.Count)
            LyricsCollection.ScrollTo(index, position: ScrollToPosition.Center, animate: !this.isInitialSync);
    }

    async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
