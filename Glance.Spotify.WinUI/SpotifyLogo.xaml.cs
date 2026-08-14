using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyLogo :
    UserControl
{
    private const string BlackLogoResource = "Glance.Spotify.WinUI.Assets.SpotifyLogoBlack.png";
    private const string GreenLogoResource = "Glance.Spotify.WinUI.Assets.SpotifyLogoGreen.png";

    private readonly BitmapImage blackLogo;
    private readonly BitmapImage greenLogo;

    public SpotifyLogo()
    {
        InitializeComponent();
        blackLogo = Load(BlackLogoResource);
        greenLogo = Load(GreenLogoResource);
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged += HandleActualThemeChanged;
        ApplyTheme();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args) =>
        ActualThemeChanged -= HandleActualThemeChanged;

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyTheme();

    private void ApplyTheme() =>
        LogoImage.Source = ActualTheme == ElementTheme.Light ? blackLogo : greenLogo;

    private static BitmapImage Load(string resourceName)
    {
        using Stream stream = typeof(SpotifyLogo).Assembly.GetManifestResourceStream(resourceName)!;
        BitmapImage image = new();
        image.SetSource(stream.AsRandomAccessStream());
        return image;
    }
}
