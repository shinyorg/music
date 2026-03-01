#if IOS || ANDROID
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shiny.Music;

/// <summary>
/// Extension methods for registering Shiny.Music services with the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMediaLibrary"/> and <see cref="IMusicPlayer"/> with the service collection.
    /// Both are registered as singletons using the platform-specific implementations.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShinyMusic(this IServiceCollection services)
    {
        services.TryAddSingleton<IMediaLibrary, MediaLibrary>();
        services.TryAddSingleton<IMusicPlayer, MusicPlayer>();
        return services;
    }
}
#endif