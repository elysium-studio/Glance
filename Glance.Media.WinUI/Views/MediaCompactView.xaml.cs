using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Media.WinUI;

public sealed partial class MediaCompactView : UserControl
{
    public MediaCompactView(MediaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => ArtworkContainer;
}
