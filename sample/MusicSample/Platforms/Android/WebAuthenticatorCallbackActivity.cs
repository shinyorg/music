using Android.App;
using Android.Content;
using Android.Content.PM;

namespace MusicSample;

// Receives the Spotify OAuth redirect (musicsample://spotify-auth) and hands it
// back to MAUI's WebAuthenticator. The scheme must match SpotifyConfig.RedirectUri.
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "musicsample",
    DataHost = "spotify-auth")]
public class WebAuthenticatorCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
