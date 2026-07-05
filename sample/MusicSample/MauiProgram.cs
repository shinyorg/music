using Shiny;
using MusicSample.Spotify;

namespace MusicSample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
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
        
        // this is static to hold the player state across tabs
        builder.Services.AddSingleton<PlayerViewModel>();

        // Spotify Web API integration (search, playlists) + App Remote playback
        builder.Services.AddShinySpotify();
        return builder.Build();
    }
}
