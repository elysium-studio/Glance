using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glance.Media.WinUI;

public sealed partial class MediaCompactView : UserControl
{
    public MediaCompactView(MediaViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public MediaViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => ArtworkContainer;

    private ImageSource? ToImageSource(object? value) => value as ImageSource;

    private Visibility WhenAvailable(bool isAvailable) =>
        isAvailable ? Visibility.Visible : Visibility.Collapsed;
}
