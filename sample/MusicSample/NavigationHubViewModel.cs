using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiny;

namespace MusicSample;

[ShellMap<NavigationHubPage>("Navigate")]
public partial class NavigationHubViewModel(INavigator navigator) : ObservableObject
{
    [RelayCommand]
    Task OpenDucking() => navigator.NavigateTo("Ducking");

    [RelayCommand]
    Task OpenSpotify() => navigator.NavigateTo("Spotify");

    [RelayCommand]
    Task OpenAiQuery() => navigator.NavigateTo("AiQuery");
}
