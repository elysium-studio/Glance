using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsCategoryView :
    UserControl
{
    private bool isRestartDialogOpen;

    public ModuleSettingsCategoryView() => InitializeComponent();

    public ModuleSettingsCategoryViewModel ViewModel => (ModuleSettingsCategoryViewModel)DataContext;

    public InfoBarSeverity GetSeverity(ModuleInstallStatusKind kind) =>
        ModuleInstallStatusSeverityConverter.Convert(kind);

    private async void HandleAddModuleClicked(object sender,
        RoutedEventArgs args)
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add(".glance");
        nint windowHandle = Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        StorageFile? file = await picker.PickSingleFileAsync();

        if (file is not null)
        {
            await InstallModulePathsAsync((string[])[file.Path]);
        }
    }

    private void HandleModulePackageDragOver(object sender,
        DragEventArgs args)
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            ModuleDropOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
        args.DragUIOverride.IsCaptionVisible = false;
        ModuleDropOverlay.Visibility = Visibility.Visible;
    }

    private void HandleModulePackageDragLeave(object sender,
        DragEventArgs args) => ModuleDropOverlay.Visibility = Visibility.Collapsed;

    private async void HandleModulePackageDrop(object sender,
        DragEventArgs args)
    {
        ModuleDropOverlay.Visibility = Visibility.Collapsed;

        if (!args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        DragOperationDeferral deferral = args.GetDeferral();
        args.AcceptedOperation = DataPackageOperation.Copy;

        try
        {
            IReadOnlyList<IStorageItem> items = await args.DataView.GetStorageItemsAsync();
            string[] packages = [.. items.OfType<StorageFile>()
                .Where(file => string.Equals(file.FileType, ".glance", StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Path)];

            if (packages.Length == 0)
            {
                ViewModel.Modules.ShowInvalidPackageStatus();
                return;
            }

            await InstallModulePathsAsync(packages);
        }
        catch (COMException exception)
        {
            ViewModel.Modules.ShowInstallFailure(exception.Message);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task InstallModulePathsAsync(IEnumerable<string> paths)
    {
        ModuleInstallResult? result = await ViewModel.Modules.InstallAsync(paths);

        if (result?.RequiresRestart == true)
        {
            await PromptForRestartAsync();
        }
    }

    private async Task PromptForRestartAsync()
    {
        if (isRestartDialogOpen || XamlRoot is null)
        {
            return;
        }

        isRestartDialogOpen = true;

        try
        {
            RestartForModuleUpdateDialog dialog = new(ViewModel.Modules.RestartDialogTitle,
                ViewModel.Modules.RestartDialogMessage,
                ViewModel.Modules.RestartDialogPrimaryButtonText,
                ViewModel.Modules.RestartDialogCloseButtonText)
            {
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AddModuleButton.IsEnabled = false;
                await ViewModel.Modules.RestartAsync();
            }
        }
        finally
        {
            isRestartDialogOpen = false;
        }
    }
}
