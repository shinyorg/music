using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.AI;
using MusicSample.Ai;
using Shiny;
using Shiny.Music;
using Shiny.Music.Extensions.AI;

namespace MusicSample;

/// <summary>
/// A chat screen that drives the <see cref="MusicAITools"/> tool surface against a GitHub Copilot
/// <see cref="IChatClient"/>. Ask it to search, play, build playlists, or jump to a song's solo — the
/// model calls the Shiny.Music tools to do it.
/// </summary>
[ShellMap<AiQueryPage>("AiQuery")]
public partial class AiQueryViewModel : ObservableObject
{
    const string SystemPrompt =
        "You are a helpful music assistant embedded in a phone app. You have tools to search and browse " +
        "the user's on-device music library, inspect a song's structure, and control playback (play, pause, " +
        "seek, playlists). Use the tools to fulfil requests such as playing a song, building a playlist, or " +
        "starting a track at its famous guitar solo. Music-library permission is already granted. Keep replies short.";

    readonly GitHubCopilotChatClientProvider copilot;
    readonly MusicAITools musicTools;
    readonly IMediaLibrary library;
    readonly List<ChatMessage> history = [];
    IChatClient? client;

    public AiQueryViewModel(GitHubCopilotChatClientProvider copilot, MusicAITools musicTools, IMediaLibrary library)
    {
        this.copilot = copilot;
        this.musicTools = musicTools;
        this.library = library;

        this.history.Add(new ChatMessage(ChatRole.System, SystemPrompt));
        this.IsSignedIn = copilot.IsAuthenticated;
        copilot.AccessTokenChanged += token =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                this.IsSignedIn = token is not null;
                if (token is not null)
                    Messages.Add(new AiMessage(false, "✅ Signed in to GitHub Copilot. Ask me anything about your music."));
            });

        // The sign-in alert is easy to miss, so also show the device code (and URL) in the chat so a long
        // poll doesn't look like a hang.
        copilot.DeviceCodeReady += (code, url) =>
            MainThread.BeginInvokeOnMainThread(() =>
                Messages.Add(new AiMessage(false,
                    $"To sign in, open {url} and enter code:\n\n{code}\n\n(It's been copied to your clipboard. Waiting for you to authorize…)")));
    }

    [ObservableProperty] bool isSignedIn;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string input = "";

    public ObservableCollection<AiMessage> Messages { get; } = [];

    [RelayCommand]
    async Task SignIn()
    {
        this.IsBusy = true;
        try
        {
            await this.copilot.GetChatClient();       // triggers the device-code flow
            this.IsSignedIn = this.copilot.IsAuthenticated;
        }
        catch (Exception ex)
        {
            Messages.Add(new AiMessage(false, "⚠ Sign-in failed: " + ex.Message));
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    [RelayCommand]
    void SignOut()
    {
        this.copilot.SignOut();
        this.client = null;
        this.IsSignedIn = false;
    }

    [RelayCommand]
    async Task Send()
    {
        var text = this.Input?.Trim();
        if (string.IsNullOrEmpty(text) || this.IsBusy)
            return;

        this.Input = "";
        Messages.Add(new AiMessage(true, text));
        this.IsBusy = true;

        try
        {
            // The tools assume library permission is already granted (they never prompt).
            await this.library.RequestPermissionAsync();

            // Build once, wrapping in the function-invocation middleware so the tool loop runs automatically.
            this.client ??= (await this.copilot.GetChatClient())
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            this.IsSignedIn = this.copilot.IsAuthenticated;

            this.history.Add(new ChatMessage(ChatRole.User, text));
            var response = await this.client.GetResponseAsync(
                this.history,
                new ChatOptions { Tools = [.. this.musicTools.Tools] }
            );
            this.history.AddMessages(response);

            var reply = string.IsNullOrWhiteSpace(response.Text)
                ? "(done)"
                : response.Text;
            Messages.Add(new AiMessage(false, reply));
        }
        catch (Exception ex)
        {
            Messages.Add(new AiMessage(false, "⚠ " + ex.Message));
        }
        finally
        {
            this.IsBusy = false;
        }
    }
}

/// <summary>A single chat bubble. Colours/alignment are precomputed so the view binds directly.</summary>
public class AiMessage
{
    public AiMessage(bool isUser, string text)
    {
        this.IsUser = isUser;
        this.Text = text;

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        this.Align = isUser ? LayoutOptions.End : LayoutOptions.Start;
        this.Background = isUser
            ? (isDark ? Color.FromArgb("#6C5CE7") : Color.FromArgb("#6C5CE7"))
            : (isDark ? Color.FromArgb("#2A2A3E") : Color.FromArgb("#FFFFFF"));
        this.TextColor = isUser
            ? Colors.White
            : (isDark ? Colors.White : Color.FromArgb("#212121"));
    }

    public bool IsUser { get; }
    public string Text { get; }
    public LayoutOptions Align { get; }
    public Color Background { get; }
    public Color TextColor { get; }
}
