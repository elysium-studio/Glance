using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;

namespace Glance.Spotify.WinUI;

public sealed partial class SpotifyLogo :
    UserControl
{
    private const string BlackLogoResource = "Glance.Spotify.WinUI.Assets.SpotifyLogoBlack.png";
    private const string GreenLogoResource = "Glance.Spotify.WinUI.Assets.SpotifyLogoGreen.png";

    private ElementTheme renderedTheme;
    private int renderedSize;

    public SpotifyLogo()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        SizeChanged += HandleSizeChanged;
    }

    internal static BitmapImage CreateImageSource(bool isLightTheme, int logicalSize) =>
        Load(isLightTheme ? BlackLogoResource : GreenLogoResource, logicalSize);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged += HandleActualThemeChanged;
        ApplyImage();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args) =>
        ActualThemeChanged -= HandleActualThemeChanged;

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyImage();

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) => ApplyImage();

    private void ApplyImage()
    {
        int logicalSize = Math.Max(1, (int)Math.Ceiling(Math.Min(ActualWidth, ActualHeight)));

        if (logicalSize == renderedSize && ActualTheme == renderedTheme)
        {
            return;
        }

        renderedSize = logicalSize;
        renderedTheme = ActualTheme;
        LogoImage.Source = CreateImageSource(ActualTheme == ElementTheme.Light, logicalSize);
    }

    private static BitmapImage Load(string resourceName, int logicalSize)
    {
        using Stream stream = typeof(SpotifyLogo).Assembly.GetManifestResourceStream(resourceName)!;
        BitmapImage image = new()
        {
            DecodePixelType = DecodePixelType.Logical,
            DecodePixelWidth = logicalSize
        };
        image.SetSource(stream.AsRandomAccessStream());
        return image;
    }
}
