using Elysium.UI.Controls.WinUI;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Torrents.WinUI;

internal enum TorrentRemovalChoice
{
    Cancel,
    RemoveFromList,
    RemoveAndDeleteData
}

internal sealed class TorrentRemovalWindow
{
    private readonly ContentDialogWindow dialog;
    private readonly WindowId ownerWindowId;

    private TorrentRemovalWindow(ModuleResourceTextLocalizer<TorrentModule> localizer, WindowId ownerWindowId)
    {
        this.ownerWindowId = ownerWindowId;

        TextBlock description = new()
        {
            Width = 600,
            Text = localizer.GetText("RemoveDescription"),
            TextWrapping = TextWrapping.Wrap,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyTextBlockStyle"] as Style
        };
        dialog = new ContentDialogWindow
        {
            Width = 680,
            Height = 260,
            Title = localizer.GetText("RemoveTitle"),
            Content = description,
            PrimaryButtonText = localizer.GetText("RemoveListOnly"),
            SecondaryButtonText = localizer.GetText("RemoveAndDelete"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
    }

    public static Task<TorrentRemovalChoice> ShowAsync(ModuleResourceTextLocalizer<TorrentModule> localizer, WindowId ownerWindowId) => new TorrentRemovalWindow(localizer, ownerWindowId).ShowAsync();

    private async Task<TorrentRemovalChoice> ShowAsync() => await dialog.ShowAsync(ownerWindowId) switch
    {
        ContentDialogResult.Primary => TorrentRemovalChoice.RemoveFromList,
        ContentDialogResult.Secondary => TorrentRemovalChoice.RemoveAndDeleteData,
        _ => TorrentRemovalChoice.Cancel
    };
}
