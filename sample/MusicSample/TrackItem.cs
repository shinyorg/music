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
    public int PlayCount => Track.PlayCount;

    [ObservableProperty] ImageSource? albumArt;

    public TrackItem(MusicMetadata track) => Track = track;

    public async Task LoadAlbumArt(IMediaLibrary library, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = await library.GetAlbumArtPathAsync(Track.Id);
            if (path != null && !cancellationToken.IsCancellationRequested)
                AlbumArt = ImageSource.FromFile(path);
        }
        catch { }
    }
}
