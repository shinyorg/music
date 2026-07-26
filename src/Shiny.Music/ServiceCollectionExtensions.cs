using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shiny.Music;

namespace Shiny;

/// <summary>
/// Extension methods for registering Shiny.Music services with the dependency injection container.
/// </summary>
public static class MusicServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMediaLibrary"/>, <see cref="IMusicPlayer"/> and <see cref="IAudioOutputDevices"/>
    /// with the service collection. All are registered as singletons using the platform-specific implementations.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShinyMusic(this IServiceCollection services)
    {
#if ANDROID
        // Android permission checks use Shiny.Core's AndroidPlatform, registered by UseShiny().
        // The consuming app must set up Shiny hosting (UseShiny / ShinyAndroidApplication) so that
        // AndroidPlatform (and the current-activity tracking behind permission requests) is available.
        services.TryAddSingleton<PlayCountStore>();
#endif
#if ANDROID || APPLE
        services.TryAddSingleton<IMediaLibrary, Shiny.Music.MediaLibrary>();
        services.TryAddSingleton<IMusicPlayer, Shiny.Music.MusicPlayer>();
        services.TryAddSingleton<IAudioOutputDevices, Shiny.Music.AudioOutputDevices>();
        services.TryAddSingleton<ILyricsProvider>(sp => new LrcLibLyricsProvider(new HttpClient()));
#endif
        return services;
    }
}