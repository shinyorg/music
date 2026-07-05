namespace Shiny.Music;

/// <summary>
/// Options that control how <see cref="IMusicPlayer.Duck"/> lowers the currently playing music.
/// </summary>
public record DuckOptions
{
    /// <summary>
    /// Target volume (0.0-1.0) while ducked. Android: applied exactly. Apple: ignored (the OS controls duck depth).
    /// </summary>
    public double Level { get; init; } = 0.2;

    /// <summary>
    /// Ramp-down duration when ducking starts. Android: honored. Apple: ignored (the OS controls the ramp).
    /// </summary>
    public TimeSpan FadeIn { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Ramp-up duration when the duck scope is disposed. Android: honored. Apple: ignored (the OS controls the ramp).
    /// </summary>
    public TimeSpan FadeOut { get; init; } = TimeSpan.FromMilliseconds(200);
}
