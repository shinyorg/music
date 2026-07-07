using Microsoft.Extensions.AI;

namespace Shiny.Music.Extensions.AI;

/// <summary>
/// Bundle of <see cref="AITool"/> instances generated for the music areas you opt-in to via
/// <c>AddMusicAITools</c>. Resolve this from DI and pass <see cref="Tools"/> to your
/// <c>IChatClient</c> call (e.g. <c>ChatOptions.Tools</c>).
/// </summary>
public sealed class MusicAITools
{
    /// <summary>The generated tools. Areas not opted-in are invisible to the LLM.</summary>
    public IReadOnlyList<AITool> Tools { get; }

    internal MusicAITools(IReadOnlyList<AITool> tools) => this.Tools = tools;
}
