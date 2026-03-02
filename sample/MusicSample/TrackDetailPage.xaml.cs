using Shiny.Music;

namespace MusicSample;

public partial class TrackDetailPage : ContentPage
{
    public TrackDetailPage(MusicMetadata track)
    {
        InitializeComponent();

        LblTitle.Text = track.Title;
        LblArtist.Text = track.Artist;
        LblAlbum.Text = track.Album;
        LblGenre.Text = track.Genre ?? "(none)";
        LblDuration.Text = track.Duration.ToString(@"mm\:ss");
        LblId.Text = track.Id;
        LblContentUri.Text = string.IsNullOrEmpty(track.ContentUri) ? "(empty — DRM protected)" : track.ContentUri;
        LblAlbumArtUri.Text = track.AlbumArtUri ?? "(none)";
    }
}
