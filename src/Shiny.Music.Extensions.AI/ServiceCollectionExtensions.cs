using Microsoft.Extensions.DependencyInjection;
using Shiny.Music;
using Shiny.Music.Extensions.AI.Internal;

namespace Shiny.Music.Extensions.AI;

/// <summary>
/// Dependency-injection extensions for exposing the Shiny.Music library and player as LLM tools.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="MusicAITools"/> singleton whose tools wrap <see cref="IMediaLibrary"/>,
    /// <see cref="IMusicPlayer"/>, and <see cref="ILyricsProvider"/> for the areas you opt-in to.
    /// Requires <c>AddShinyMusic()</c> to have registered those services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Builder callback used to opt-in music areas.</param>
    /// <remarks>
    /// The generated tools assume music-library permission is already granted — they do not trigger
    /// the platform permission UI (which needs a foreground activity). Call
    /// <c>IMediaLibrary.RequestPermissionAsync</c> from your app before invoking the agent.
    /// </remarks>
    public static IServiceCollection AddMusicAITools(
        this IServiceCollection services,
        Action<IMusicAIToolBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new MusicAIToolBuilder();
        configure(builder);

        if (builder.IsEmpty)
            throw new InvalidOperationException(
                "AddMusicAITools requires at least one Add… call. " +
                "An empty registration would expose no tools to the LLM.");

        services.AddSingleton(sp =>
        {
            var library = sp.GetRequiredService<IMediaLibrary>();
            var player = sp.GetService<IMusicPlayer>();
            var lyrics = sp.GetService<ILyricsProvider>();
            var tools = MusicAIFunctionFactory.Build(library, player, lyrics, builder);
            return new MusicAITools(tools);
        });

        return services;
    }
}
