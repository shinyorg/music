using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiny.Spotify.Maui;

namespace Shiny;

/// <summary>
/// Extension methods for registering Shiny.Spotify services with the dependency injection container.
/// </summary>
public static class SpotifyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Spotify Web API client, OAuth (PKCE) auth service, and the platform-specific
    /// <see cref="ISpotifyRemote"/> App Remote implementation. On platforms without the App Remote SDK,
    /// an <see cref="UnavailableSpotifyRemote"/> is registered so DI resolution still succeeds.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShinySpotify(this IServiceCollection services)
    {
        services.TryAddSingleton<SpotifyAuthService>();
        services.TryAddSingleton<SpotifyClient>();
#if ANDROID
        services.TryAddSingleton<ISpotifyRemote, SpotifyRemoteAndroid>();
#elif IOS
        services.TryAddSingleton<ISpotifyRemote, SpotifyRemoteApple>();
#else
        services.TryAddSingleton<ISpotifyRemote, UnavailableSpotifyRemote>();
#endif
        return services;
    }
}
