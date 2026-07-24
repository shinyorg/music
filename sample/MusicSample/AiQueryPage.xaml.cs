using System.Collections.Specialized;

namespace MusicSample;

public partial class AiQueryPage : ContentPage
{
    public AiQueryPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AiQueryViewModel vm)
            vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is AiQueryViewModel vm)
            vm.Messages.CollectionChanged -= OnMessagesChanged;
    }

    void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (BindingContext is AiQueryViewModel vm && vm.Messages.Count > 0)
            Transcript.ScrollTo(vm.Messages.Count - 1, position: ScrollToPosition.End, animate: true);
    }

    void OnEntryCompleted(object? sender, EventArgs e)
    {
        if (BindingContext is AiQueryViewModel vm && vm.SendCommand.CanExecute(null))
            vm.SendCommand.Execute(null);
    }
}
