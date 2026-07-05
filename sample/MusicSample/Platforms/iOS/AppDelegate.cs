using Foundation;
using UIKit;

namespace MusicSample;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // Routes the musicsample://spotify-auth redirect: App Remote token first
    // (returns from the Spotify app), otherwise the Web API PKCE code flow.
    public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
    {
        if (Shiny.Spotify.Maui.SpotifyRemoteApple.Current?.HandleAuthCallback(url) == true)
            return true;

        if (Microsoft.Maui.Authentication.WebAuthenticator.Default.OpenUrl(new Uri(url.AbsoluteString!)))
            return true;

        return base.OpenUrl(app, url, options);
    }
}
