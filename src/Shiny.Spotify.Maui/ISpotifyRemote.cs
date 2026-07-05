namespace Shiny.Spotify.Maui;

/// <summary>Snapshot of the Spotify app's player, delivered as playback changes.</summary>
public record RemotePlayerState(
    string? TrackName,
    string? ArtistName,
    string? TrackUri,
    bool IsPaused,
    long PositionMs
);

/// <summary>
/// Controls playback through the installed Spotify app via the native App Remote
/// SDK. Requires the Spotify app installed + a Premium account. Implemented per
/// platform over the Android/iOS App Remote bindings.
/// </summary>
public interface ISpotifyRemote
{
    bool IsConnected { get; }

    /// <summary>Connects to the Spotify app, auto-launching it if needed.</summary>
    Task ConnectAsync();

    void Disconnect();

    Task PlayAsync(string spotifyUri);
    Task ResumeAsync();
    Task PauseAsync();
    Task SkipNextAsync();
    Task SkipPreviousAsync();
    Task SeekAsync(long positionMs);

    /// <summary>Raised whenever the Spotify app's player state changes.</summary>
    event EventHandler<RemotePlayerState>? PlayerStateChanged;
}

/// <summary>Used on platforms where the App Remote SDK binaries have not been added yet.</summary>
public sealed class UnavailableSpotifyRemote : ISpotifyRemote
{
    public bool IsConnected => false;
    public event EventHandler<RemotePlayerState>? PlayerStateChanged { add { } remove { } }

    static Task Fail() => throw new PlatformNotSupportedException(
        "The Spotify App Remote SDK is not available on this platform build. " +
        "Add the native SDK binaries and the platform binding project reference.");

    public Task ConnectAsync() => Fail();
    public void Disconnect() { }
    public Task PlayAsync(string spotifyUri) => Fail();
    public Task ResumeAsync() => Fail();
    public Task PauseAsync() => Fail();
    public Task SkipNextAsync() => Fail();
    public Task SkipPreviousAsync() => Fail();
    public Task SeekAsync(long positionMs) => Fail();
}
