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

        builder.Services.AddSingleton<IMediaLibrary, MediaLibrary>();
        builder.Services.AddSingleton<IMusicPlayer, MusicPlayer>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
