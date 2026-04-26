using CommunityToolkit.Mvvm.ComponentModel;
using Shiny.Music;

namespace MusicSample;

public partial class TrackItem : ObservableObject
{
    public MusicMetadata Track { get; }
    public string? Title => Track.Title;
    public string? Artist => Track.Artist;
    public string? Album => Track.Album;
    public TimeSpan Duration => Track.Duration;

    [ObservableProperty] ImageSource? albumArt;

    public TrackItem(MusicMetadata track) => Track = track;

    public async Task LoadAlbumArt(IMediaLibrary library)
    {
        try
        {
            var path = await library.GetAlbumArtPathAsync(Track.Id);
            if (path != null)
                AlbumArt = ImageSource.FromFile(path);
        }
        catch { }
    }
}
