using ObjCRuntime;

namespace Shiny.Spotify.AppRemote.iOS;

[Native]
public enum SPTAppRemoteLogLevel : long
{
    None = 0,
    Debug,
    Info,
    Error
}
