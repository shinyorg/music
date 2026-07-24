namespace MusicSample;

public partial class WaveformPage : ContentPage
{
    readonly WaveformViewModel vm;

    public WaveformPage(WaveformViewModel viewModel)
    {
        InitializeComponent();
        this.vm = viewModel;
        BindingContext = viewModel;

        WaveformView.Drawable = new WaveformDrawable(viewModel);
        VuView.Drawable = new VuMeterDrawable(viewModel);
    }

    bool navigatingAway;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        this.vm.SetDispatcher(Dispatcher);
        this.vm.Invalidated += OnInvalidated;
        this.vm.Unavailable += OnUnavailable;
        await this.vm.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        this.vm.Invalidated -= OnInvalidated;
        this.vm.Unavailable -= OnUnavailable;
        this.vm.Stop();
    }

    async void OnUnavailable(object? sender, string message)
    {
        if (this.navigatingAway)
            return;
        this.navigatingAway = true;

        await DisplayAlert("Waveform unavailable", message, "OK");
        await Navigation.PopModalAsync();
    }

    void OnInvalidated(object? sender, EventArgs e)
    {
        WaveformView.Invalidate();
        VuView.Invalidate();
    }

    // ── Waveform touch → seek ────────────────────────────────────

    void OnWaveStart(object? sender, TouchEventArgs e)
    {
        this.vm.BeginScrub();
        this.vm.ScrubTo(FractionFor(sender, e));
    }

    void OnWaveDrag(object? sender, TouchEventArgs e)
        => this.vm.ScrubTo(FractionFor(sender, e));

    void OnWaveEnd(object? sender, TouchEventArgs e)
        => this.vm.EndScrub(FractionFor(sender, e));

    static double FractionFor(object? sender, TouchEventArgs e)
    {
        if (sender is not GraphicsView view || e.Touches.Length == 0 || view.Width <= 0)
            return 0;

        return Math.Clamp(e.Touches[0].X / view.Width, 0, 1);
    }

    async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopModalAsync();
}
