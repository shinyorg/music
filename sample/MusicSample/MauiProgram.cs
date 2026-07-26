using Shiny;
using MusicSample.Ai;
using MusicSample.Spotify;
using Shiny.Music.Extensions.AI;

namespace MusicSample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseShiny()
            .UseShinyShell(x => x
                .AddGeneratedMaps()
                .UseUxDiversDialogs()
            )
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddShinyMusic();

        // GitHub Copilot sign-in (device-code flow) + the Shiny.Music AI tool surface, so the AI screen
        // can run tool-calling music queries against a Copilot IChatClient.
        builder.Services.AddSingleton<GitHubCopilotChatClientProvider>();
        builder.Services.AddMusicAITools(tools =>
        {
            tools.AddLibrary().AddPlayback().AddPlaylistManagement();
#if IOS || MACCATALYST
            tools.AddCatalog();   // Apple Music streaming catalog search (Apple-only)
#endif
        });

        // this is static to hold the player state across tabs
        builder.Services.AddSingleton<PlayerViewModel>();

        // Spotify Web API integration (search, playlists) + App Remote playback
        builder.Services.AddShinySpotify();
        return builder.Build();
    }
}
