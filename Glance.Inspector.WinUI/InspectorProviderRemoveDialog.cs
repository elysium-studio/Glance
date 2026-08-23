using Microsoft.UI.Xaml.Controls;

namespace Glance.Inspector.WinUI;

public sealed class InspectorProviderRemoveDialog :
    ContentDialog
{
    public InspectorProviderRemoveDialog(string title, string message, string primaryButtonText, string closeButtonText)
    {
        Title = title;
        Content = new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords };
        PrimaryButtonText = primaryButtonText;
        CloseButtonText = closeButtonText;
        DefaultButton = ContentDialogButton.Close;
    }
}
