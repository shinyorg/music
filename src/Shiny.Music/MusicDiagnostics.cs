namespace Shiny.Music;

/// <summary>
/// Optional diagnostics hook. Subscribe to <see cref="Message"/> to receive human-readable notes about
/// internal operations — currently why <see cref="IMediaLibrary.AnalyzeLevelsAsync"/> returned <c>null</c>,
/// and timing for library queries. Nothing is emitted unless a handler is attached, so it has no cost in
/// production. Handlers may be invoked on a background thread.
/// </summary>
public static class MusicDiagnostics
{
    /// <summary>Raised with a diagnostic message. For debugging/observability only.</summary>
    public static event Action<string>? Message;

    internal static bool IsEnabled => Message != null;

    internal static void Log(string message) => Message?.Invoke(message);
}
