namespace Shiny.Music;

/// <summary>
/// The disposable returned by <see cref="IMusicPlayer.Duck"/>. Invokes its restore callback at most once,
/// on first disposal. The player decides (via the callback) whether this scope is still the active one.
/// </summary>
sealed class DuckScope(Func<DuckScope, ValueTask> onDispose) : IAsyncDisposable
{
    Func<DuckScope, ValueTask>? onDispose = onDispose;

    /// <summary>A scope that does nothing when disposed (returned when there is nothing to duck).</summary>
    public static DuckScope NoOp { get; } = new(_ => default);

    public ValueTask DisposeAsync()
    {
        var cb = Interlocked.Exchange(ref this.onDispose, null);
        return cb?.Invoke(this) ?? default;
    }
}
