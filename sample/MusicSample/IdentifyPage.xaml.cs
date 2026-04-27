using System.Globalization;

namespace MusicSample;

public partial class IdentifyPage : ContentPage
{
    public IdentifyPage()
    {
        InitializeComponent();
    }

    async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class ListeningIconConverter : IValueConverter, IMarkupExtension
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "⏹" : "🎵";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object ProvideValue(IServiceProvider serviceProvider) => this;
}

public class ListeningTextConverter : IValueConverter, IMarkupExtension
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Tap to cancel" : "Tap to listen";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object ProvideValue(IServiceProvider serviceProvider) => this;
}

public class NotNullConverter : IValueConverter, IMarkupExtension
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object ProvideValue(IServiceProvider serviceProvider) => this;
}
