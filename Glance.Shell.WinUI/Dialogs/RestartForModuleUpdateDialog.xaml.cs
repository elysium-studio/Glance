using Microsoft.UI.Xaml.Controls;

namespace Glance.Shell.WinUI;

public sealed partial class RestartForModuleUpdateDialog :
    ContentDialog
{
    public RestartForModuleUpdateDialog(string title,
        string message,
        string primaryButtonText,
        string closeButtonText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        PrimaryButtonText = primaryButtonText;
        CloseButtonText = closeButtonText;
    }
}
