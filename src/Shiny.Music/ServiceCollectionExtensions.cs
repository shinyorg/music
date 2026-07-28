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
    /// <param name="configure">
    /// Optional configuration of <see cref="MusicPlayerOptions"/> - background playback, the Android media
    /// notification, audio focus behavior, and interruption auto-resume. Defaults are applied when omitted.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShinyMusic(
        this IServiceCollection services,
        Action<MusicPlayerOptions>? configure = null
    )
    {
        var options = new MusicPlayerOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

#if ANDROID
        // Android permission checks use Shiny.Core's AndroidPlatform, registered by UseShiny().
        // The consuming app must set up Shiny hosting (UseShiny / ShinyAndroidApplication) so that
        // AndroidPlatform (and the current-activity tracking behind permission requests) is available.
        services.TryAddSingleton<PlayCountStore>();

        // The local-file engine. Additional backends (e.g. the Apple Music catalog player in
        // Shiny.Music.Android.AppleMusicKit) append themselves to this same collection with
        // TryAddEnumerable; MusicPlayer routes each track to the first one that can play it.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Shiny.Music.IPlaybackBackend, Shiny.Music.MediaPlayerBackend>()
        );

        // Constructed by hand rather than by type: MusicPlayer's constructor is internal (it takes the
        // internal backend seam) and ActivatorUtilities only considers public constructors.
        services.TryAddSingleton<IMusicPlayer>(sp => new Shiny.Music.MusicPlayer(
            sp.GetRequiredService<AndroidPlatform>(),
            sp.GetRequiredService<PlayCountStore>(),
            sp.GetRequiredService<MusicPlayerOptions>(),
            sp.GetServices<Shiny.Music.IPlaybackBackend>()
        ));
#endif
#if APPLE
        services.TryAddSingleton<IMusicPlayer, Shiny.Music.MusicPlayer>();
#endif
#if ANDROID || APPLE
        services.TryAddSingleton<IMediaLibrary, Shiny.Music.MediaLibrary>();
        services.TryAddSingleton<IAudioOutputDevices, Shiny.Music.AudioOutputDevices>();
        services.TryAddSingleton<ILyricsProvider>(sp => new LrcLibLyricsProvider(new HttpClient()));
#endif
        return services;
    }
}
