using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Glance.Torrents.WinUI;

public sealed partial class TorrentSettingsView : UserControl
{
    public TorrentSettingsView() => InitializeComponent();

    private async void BrowseClicked(object sender, RoutedEventArgs args)
    {
        if (DataContext is not TorrentSettingsViewModel viewModel || XamlRoot is null) return;
        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        WindowId id = XamlRoot.ContentIslandEnvironment.AppWindowId;
        nint hwnd = Win32Interop.GetWindowFromWindowId(id);
        InitializeWithWindow.Initialize(picker, hwnd);
        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null) viewModel.DownloadPath = folder.Path;
    }

    private async void SaveClicked(object sender, RoutedEventArgs args)
    {
        if (DataContext is TorrentSettingsViewModel viewModel) _ = await viewModel.SaveAsync();
    }
}
