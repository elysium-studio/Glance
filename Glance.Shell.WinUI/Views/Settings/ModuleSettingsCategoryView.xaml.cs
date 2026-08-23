using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsCategoryView :
    UserControl
{
    private readonly DispatcherQueue dispatcherQueue;
    private bool isRestartDialogOpen;

    public ModuleSettingsCategoryView()
    {
        InitializeComponent();
        dispatcherQueue = DispatcherQueue;
    }

    public ModuleSettingsCategoryViewModel ViewModel => (ModuleSettingsCategoryViewModel)DataContext;

    public InfoBarSeverity GetSeverity(ModuleInstallStatusKind kind) =>
        ModuleInstallStatusSeverityConverter.Convert(kind);

    private async void HandleRestartClicked(object sender, RoutedEventArgs args)
    {
        RestartButton.IsEnabled = false;

        try
        {
            await ViewModel.Modules.RestartAsync();
        }
        finally
        {
            RestartButton.IsEnabled = true;
        }
    }

    private async void HandleAddModuleClicked(object sender,
        RoutedEventArgs args)
    {
        ModulesViewModel modules = ViewModel.Modules;
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
            string path = await RunOnDispatcherAsync(() => file.Path);
            await InstallModulePathsAsync(modules, (string[])[path]);
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
        DataPackageView dataView = args.DataView;
        ModulesViewModel modules = ViewModel.Modules;
        args.AcceptedOperation = DataPackageOperation.Copy;

        try
        {
            IReadOnlyList<IStorageItem> items = await dataView.GetStorageItemsAsync();
            string[] packages = await RunOnDispatcherAsync(() =>
                items.OfType<StorageFile>()
                    .Where(file => string.Equals(file.FileType, ".glance", StringComparison.OrdinalIgnoreCase))
                    .Select(file => file.Path)
                    .ToArray());

            if (packages.Length == 0)
            {
                modules.ShowInvalidPackageStatus();
                return;
            }

            await InstallModulePathsAsync(modules, packages);
        }
        catch
        {
            modules.ShowInstallFailure();
        }
        finally
        {
            await RunOnDispatcherAsync(deferral.Complete);
        }
    }

    private async Task InstallModulePathsAsync(ModulesViewModel modules,
        IEnumerable<string> paths)
    {
        ModuleInstallResult? result = await modules.InstallAsync(paths);

        if (result?.RequiresRestart == true)
        {
            await PromptForRestartAsync(modules);
        }
    }

    private Task PromptForRestartAsync(ModulesViewModel modules) => RunTaskOnDispatcherAsync(async () =>
    {
        if (isRestartDialogOpen || XamlRoot is null)
        {
            return;
        }

        isRestartDialogOpen = true;

        try
        {
            if (await modules.ConfirmRestartAsync(XamlRoot))
            {
                await RunOnDispatcherAsync(() => AddModuleButton.IsEnabled = false);
                await modules.RestartAsync();
            }
        }
        finally
        {
            await RunOnDispatcherAsync(() => isRestartDialogOpen = false);
        }
    });

    private Task RunOnDispatcherAsync(Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The module settings dispatcher rejected the operation."));
        }

        return completion.Task;
    }

    private Task RunTaskOnDispatcherAsync(Func<Task> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The module settings dispatcher rejected the operation."));
        }

        return completion.Task;
    }

    private Task<T> RunOnDispatcherAsync<T>(Func<T> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(action());
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    completion.TrySetResult(action());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The module settings dispatcher rejected the operation."));
        }

        return completion.Task;
    }
}
