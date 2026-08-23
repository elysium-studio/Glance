using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Glance.Inspector.WinUI;

public sealed partial class InspectorProviderSettingsView :
    UserControl
{
    public InspectorProviderSettingsView() => InitializeComponent();

    public InspectorProviderSettingsViewModel ViewModel => (InspectorProviderSettingsViewModel)DataContext;

    public InfoBarSeverity GetStatusSeverity(bool isError) => isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;

    private async void HandleAddClicked(object sender, RoutedEventArgs args)
    {
        FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".glance");
        nint windowHandle = Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        StorageFile? file = await picker.PickSingleFileAsync();

        if (file is not null)
        {
            await ViewModel.InstallAsync(file.Path);
        }
    }

    private async void HandleRemoveClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: InspectorProviderSettingItemViewModel provider })
        {
            return;
        }

        if (await ViewModel.ConfirmRemoveAsync(XamlRoot))
        {
            await ViewModel.RemoveAsync(provider);
        }
    }
}
