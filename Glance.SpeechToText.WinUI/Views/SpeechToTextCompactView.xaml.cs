using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.SpeechToText.WinUI;

public sealed partial class SpeechToTextCompactView :
    UserControl
{
    public SpeechToTextCompactView(SpeechToTextViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SpeechToTextViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;
}
