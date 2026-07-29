using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Glance.Media.WinUI;

public sealed partial class MediaExpandedView :
    UserControl
{
    public MediaExpandedView(MediaViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MediaViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => ArtworkContainer;

    private ImageSource? ToImageSource(object? value) => value as ImageSource;

    private SolidColorBrush ToAccentBrush(uint color) => MediaAccentPalette.GetBrush(color);

    private Color ToAccentColor(uint color) => MediaAccentPalette.GetAccent(color);

    private Color ToPointerOverColor(uint color) => MediaAccentPalette.GetPointerOver(color);

    private Color ToPressedColor(uint color) => MediaAccentPalette.GetPressed(color);

    private Color ToDisabledColor(uint color) => MediaAccentPalette.GetDisabled(color);

    private Color ToForegroundColor(uint color) => MediaAccentPalette.GetForeground(color);

    private Color ToPointerOverForegroundColor(uint color) => MediaAccentPalette.GetPointerOverForeground(color);

    private Color ToPressedForegroundColor(uint color) => MediaAccentPalette.GetPressedForeground(color);

    private Color ToDisabledForegroundColor(uint color) => MediaAccentPalette.GetDisabledForeground(color);

    private Color ToBorderColor(uint color) => MediaAccentPalette.GetBorder(color);

    private Color ToPointerOverBorderColor(uint color) => MediaAccentPalette.GetPointerOverBorder(color);

    private Color ToPressedBorderColor(uint color) => MediaAccentPalette.GetPressedBorder(color);

    private Color ToDisabledBorderColor(uint color) => MediaAccentPalette.GetDisabledBorder(color);

    private Visibility WhenArtworkUnavailable(object? artwork) =>
        artwork is null ? Visibility.Visible : Visibility.Collapsed;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenAvailable(bool isAvailable) =>
        isAvailable ? Visibility.Visible : Visibility.Collapsed;
}
