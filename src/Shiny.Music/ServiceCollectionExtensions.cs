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
    /// Registers <see cref="IMediaLibrary"/> and <see cref="IMusicPlayer"/> with the service collection.
    /// Both are registered as singletons using the platform-specific implementations.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShinyMusic(this IServiceCollection services)
    {
#if ANDROID
        services.TryAddSingleton<ActivityProvider>();
#endif
#if ANDROID || IOS
        services.TryAddSingleton<IMediaLibrary, MediaLibrary>();
        services.TryAddSingleton<IMusicPlayer, MusicPlayer>();
        services.TryAddSingleton<ILyricsProvider>(sp => new LrcLibLyricsProvider(new HttpClient()));
#endif
#if IOS
        services.TryAddSingleton<IMusicIdentifier, MusicIdentifier>();
#endif
        return services;
    }
}