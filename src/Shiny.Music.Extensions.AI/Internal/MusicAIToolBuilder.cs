namespace Shiny.Music.Extensions.AI.Internal;

sealed class MusicAIToolBuilder : IMusicAIToolBuilder
{
    public bool Library { get; private set; }
    public bool Playback { get; private set; }
    public bool PlaylistManagement { get; private set; }
    public bool Catalog { get; private set; }

    public bool IsEmpty => !this.Library && !this.Playback && !this.PlaylistManagement && !this.Catalog;

    public IMusicAIToolBuilder AddLibrary()
    {
        this.Library = true;
        return this;
    }

    public IMusicAIToolBuilder AddPlayback()
    {
        this.Playback = true;
        return this;
    }

    public IMusicAIToolBuilder AddPlaylistManagement()
    {
        this.PlaylistManagement = true;
        return this;
    }

    public IMusicAIToolBuilder AddCatalog()
    {
        this.Catalog = true;
        return this;
    }

    public IMusicAIToolBuilder AddAll()
    {
        this.Library = true;
        this.Playback = true;
        this.PlaylistManagement = true;
        return this;
    }
}
