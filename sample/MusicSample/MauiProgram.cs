using Shiny.Music;

namespace MusicSample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddShinyMusic();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<GenresPage>();
        builder.Services.AddTransient<DecadesPage>();
        builder.Services.AddTransient<YearsPage>();
        builder.Services.AddTransient<PlaylistsPage>();

        return builder.Build();
    }
}
