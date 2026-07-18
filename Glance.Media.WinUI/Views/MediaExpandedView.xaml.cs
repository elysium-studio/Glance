using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Media.WinUI;

public sealed partial class MediaExpandedView : UserControl
{
    public MediaExpandedView(MediaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public FrameworkElement ConnectedAnimationElement => ArtworkContainer;
}
