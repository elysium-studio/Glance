using Windows.ApplicationModel.DataTransfer;

namespace Glance.ColorPicker.WinUI;

public sealed class WindowsTextCopyService :
    ITextCopyService
{
    public void Copy(string text)
    {
        DataPackage package = new();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush();
    }
}
