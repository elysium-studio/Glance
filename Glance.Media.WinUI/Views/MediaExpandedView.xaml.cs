using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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

    private Visibility WhenArtworkUnavailable(object? artwork) =>
        artwork is null ? Visibility.Visible : Visibility.Collapsed;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private Visibility WhenAvailable(bool isAvailable) =>
        isAvailable ? Visibility.Visible : Visibility.Collapsed;
}
