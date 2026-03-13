namespace MusicSample;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("trackdetail", typeof(TrackDetailPage));
        Routing.RegisterRoute("tracks", typeof(TracksPage));
    }
}
