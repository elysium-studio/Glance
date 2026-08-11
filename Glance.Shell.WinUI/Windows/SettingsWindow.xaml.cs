using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsWindow :
    Window,
    IRecipient<SettingsNavigationRequestedEventArgs>,
    IRecipient<ModuleUninstalledEventArgs>
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 680;
    private readonly IApplicationLifetime applicationLifetime;
    private readonly AboutViewModel aboutViewModel;
    private readonly ITextLocalizer localizer;
    private readonly ModuleInstallationService moduleInstallations;
    private readonly IMessenger messenger;
    private readonly Dictionary<ISettingViewModel, NavigationViewItem> navigationItems = [];
    private readonly List<ISettingViewModel> navigationPath = [];
    private readonly List<INotifyCollectionChanged> observedNavigationCollections = [];
    private bool isBuildingNavigation;
    private bool isNavigationRebuildPending;
    private bool isAboutDialogOpen;
    private bool isClosing;
    private bool isQuitDialogOpen;
    private bool isRestartDialogOpen;

    public SettingsWindow(IMessenger messenger,
        ITextLocalizer localizer,
        IApplicationLifetime applicationLifetime,
        AboutViewModel aboutViewModel,
        ModuleInstallationService moduleInstallations)
    {
        InitializeComponent();

        this.messenger = messenger;
        this.localizer = localizer;
        this.applicationLifetime = applicationLifetime;
        this.aboutViewModel = aboutViewModel;
        this.moduleInstallations = moduleInstallations;

        messenger.Register<SettingsNavigationRequestedEventArgs>(this);
        messenger.Register<ModuleUninstalledEventArgs>(this);
        Closed += HandleClosed;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        OverlappedPresenter presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;

        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);

        int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width / 2) - (WindowWidth / 2);
        int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height / 2) - (WindowHeight / 2);

        AppWindow.MoveAndResize(new RectInt32(centeredX, centeredY, WindowWidth, WindowHeight));
    }

    public ObservableCollection<string> BreadcrumbItems { get; } = [];

    public SettingsViewModel ViewModel => field ??= (SettingsViewModel)((FrameworkElement)Content).DataContext;

    public void Receive(SettingsNavigationRequestedEventArgs message)
    {
        if (isClosing)
        {
            return;
        }

        List<ISettingViewModel>? path = FindNavigationPath(message.Parent);

        if (path is not null)
        {
            path.Add(message.Target);
            Navigate(path);
        }
    }

    private void HandleLoaded(object sender,
        RoutedEventArgs args)
    {
        if (!isClosing &&
            ((FrameworkElement)Content).DataContext is SettingsViewModel)
        {
            BuildNavigation();
        }
    }

    public void Receive(ModuleUninstalledEventArgs message) => _ = RunOnDispatcherAsync(() =>
    {
        if (!isClosing)
        {
            ShowModuleInstallStatus(InfoBarSeverity.Warning,
                localizer.GetText("ModuleRemovedMessage", string.Join(", ", message.DisplayNames)));
        }
    });

    private async void HandleAddModuleClicked(object sender,
        RoutedEventArgs args)
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };
        picker.FileTypeFilter.Add(".glance");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSingleFileAsync();

        if (file is not null)
        {
            await InstallModulePathsAsync((string[])[file.Path]);
        }
    }

    private void HandleModulePackageDragOver(object sender,
        DragEventArgs args)
    {
        if (!IsModuleSettingsVisible() || !args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            ModuleDropOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
        args.DragUIOverride.IsCaptionVisible = true;
        args.DragUIOverride.Caption = localizer.GetText("ModuleDropTargetText/Text");
        ModuleDropOverlay.Visibility = Visibility.Visible;
    }

    private void HandleModulePackageDragLeave(object sender,
        DragEventArgs args) => ModuleDropOverlay.Visibility = Visibility.Collapsed;

    private async void HandleModulePackageDrop(object sender,
        DragEventArgs args)
    {
        ModuleDropOverlay.Visibility = Visibility.Collapsed;

        if (!IsModuleSettingsVisible() || !args.DataView.Contains(StandardDataFormats.StorageItems))
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
                await RunOnDispatcherAsync(() => ShowModuleInstallStatus(InfoBarSeverity.Error,
                    localizer.GetText("ModuleInstallInvalidPackageMessage")));
                return;
            }

            await InstallModulePathsAsync(packages);
        }
        catch (COMException exception)
        {
            await RunOnDispatcherAsync(() => ShowModuleInstallStatus(InfoBarSeverity.Error, exception.Message));
        }
        finally
        {
            await RunOnDispatcherAsync(deferral.Complete);
        }
    }

    private async Task InstallModulePathsAsync(IEnumerable<string> paths)
    {
        await RunOnDispatcherAsync(() =>
        {
            AddModuleButton.IsEnabled = false;
            ModuleInstallInfoBar.IsOpen = false;
        });
        ModuleInstallResult? lastResult = null;
        bool restartRequired = false;

        try
        {
            foreach (string path in paths)
            {
                ModuleInstallResult result = await moduleInstallations.InstallAsync(path);

                if (!result.IsSuccessful)
                {
                    await RunOnDispatcherAsync(() => ShowModuleInstallStatus(InfoBarSeverity.Error,
                        string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? localizer.GetText("ModuleInstallFailedMessage")
                            : result.ErrorMessage));
                    return;
                }

                lastResult = result;
                restartRequired |= result.RequiresRestart;
            }

            if (lastResult is not null)
            {
                ModuleInstallResult installedResult = lastResult;
                bool requiresRestart = restartRequired;
                string installedModuleNames = ResolveInstalledModuleNames(installedResult);

                await RunOnDispatcherAsync(() =>
                {
                    NavigateToInstalledModule(installedResult);
                    ShowModuleInstallStatus(InfoBarSeverity.Success,
                        requiresRestart
                            ? localizer.GetText("ModuleUpdateStagedMessage", installedModuleNames)
                            : localizer.GetText("ModuleInstalledMessage", installedModuleNames));
                });

                if (requiresRestart)
                {
                    await PromptForModuleUpdateRestartAsync();
                }
            }
        }
        catch (Exception exception)
        {
            await RunOnDispatcherAsync(() => ShowModuleInstallStatus(InfoBarSeverity.Error,
                string.IsNullOrWhiteSpace(exception.Message)
                    ? localizer.GetText("ModuleInstallFailedMessage")
                    : exception.Message));
        }
        finally
        {
            await RunOnDispatcherAsync(() => AddModuleButton.IsEnabled = true);
        }
    }

    private async Task PromptForModuleUpdateRestartAsync()
    {
        if (isRestartDialogOpen || isClosing)
        {
            return;
        }

        isRestartDialogOpen = true;

        try
        {
            if (await ShowModuleUpdateRestartDialogAsync() == ContentDialogResult.Primary)
            {
                await RestartApplicationAsync();
            }
        }
        finally
        {
            isRestartDialogOpen = false;
        }
    }

    private async Task RestartApplicationAsync()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The Glance executable path is not available.");

        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add("--restart-after");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Glance could not start the replacement process.");

        await RunOnDispatcherAsync(() =>
        {
            isClosing = true;
            AddModuleButton.IsEnabled = false;
            Close();
        });
        await applicationLifetime.ExitAsync();
    }

    private Task<ContentDialogResult> ShowModuleUpdateRestartDialogAsync()
    {
        TaskCompletionSource<ContentDialogResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void ShowDialog()
        {
            RestartForModuleUpdateDialog dialog = new(localizer)
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot
            };

            _ = CompleteAsync();

            async Task CompleteAsync()
            {
                try
                {
                    completion.TrySetResult(await dialog.ShowAsync());
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            ShowDialog();
        }
        else if (!DispatcherQueue.TryEnqueue(ShowDialog))
        {
            completion.TrySetException(new InvalidOperationException("The settings dispatcher rejected the module restart dialog."));
        }

        return completion.Task;
    }

    private Task RunOnDispatcherAsync(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!DispatcherQueue.TryEnqueue(() =>
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
            completion.TrySetException(new InvalidOperationException("The settings dispatcher rejected the operation."));
        }

        return completion.Task;
    }

    private void NavigateToInstalledModule(ModuleInstallResult result)
    {
        ModulesViewModel? modules = ViewModel.OfType<ModulesViewModel>().FirstOrDefault();
        SettingsCategoryViewModel? category = result.ComponentIds
            .Select(componentId => modules?.FindCategoryForComponent(componentId))
            .FirstOrDefault(candidate => candidate is not null);

        if (category is null)
        {
            return;
        }

        List<ISettingViewModel>? path = FindNavigationPath(category);

        if (path is null)
        {
            return;
        }

        if (navigationItems.TryGetValue(category, out NavigationViewItem? item))
        {
            SettingsNavigation.SelectedItem = item;
        }

        Navigate(path);
    }

    private string ResolveInstalledModuleNames(ModuleInstallResult result)
    {
        ModulesViewModel? modules = ViewModel.OfType<ModulesViewModel>().FirstOrDefault();

        return string.Join(", ", result.ComponentIds.Select(componentId =>
            modules?.FindDisplayNameForComponent(componentId) ?? componentId));
    }

    private void ShowModuleInstallStatus(InfoBarSeverity severity,
        string message)
    {
        ModuleInstallInfoBar.Severity = severity;
        ModuleInstallInfoBar.Message = message;
        ModuleInstallInfoBar.IsOpen = true;
    }

    private bool IsModuleSettingsVisible() => navigationPath.FirstOrDefault() is ModulesViewModel;

    private void HandleNavigationSelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (isClosing)
        {
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item ||
            item.Tag is not ISettingViewModel selectedItem)
        {
            return;
        }

        List<ISettingViewModel>? path = FindNavigationPath(selectedItem);

        if (path is null)
        {
            return;
        }

        if (selectedItem.Children.Count > 0)
        {
            path.Add(selectedItem.Children[0]);
            SettingsNavigation.SelectedItem = navigationItems[selectedItem.Children[0]];
        }

        Navigate(path);
    }

    private async void HandleQuitTapped(object sender,
        Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isQuitDialogOpen)
        {
            return;
        }

        isQuitDialogOpen = true;

        try
        {
            QuitDialog dialog = new(localizer)
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                isClosing = true;
                QuitGlanceNavigationItem.IsEnabled = false;
                Close();
                await applicationLifetime.ExitAsync();
            }
        }
        finally
        {
            isQuitDialogOpen = false;
        }
    }

    private async void HandleAboutTapped(object sender,
        Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isAboutDialogOpen)
        {
            return;
        }

        isAboutDialogOpen = true;

        try
        {
            AboutDialog dialog = new(aboutViewModel,
                localizer)
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot
            };
            _ = await dialog.ShowAsync();
        }
        finally
        {
            isAboutDialogOpen = false;
        }
    }

    private void HandleBackRequested(TitleBar sender,
        object args) => GoBack();

    private void HandleBreadcrumbItemClicked(BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index < 0 ||
            args.Index >= navigationPath.Count - 1)
        {
            return;
        }

        List<ISettingViewModel> path = [.. navigationPath.Take(args.Index + 1)];
        ISettingViewModel target = path[^1];

        if (target.Children.Count > 0)
        {
            target = target.Children[0];
            path.Add(target);
        }

        if (navigationItems.TryGetValue(target, out NavigationViewItem? item))
        {
            SettingsNavigation.SelectedItem = item;
        }

        Navigate(path);
    }

    private void HandleClosed(object sender,
        WindowEventArgs args)
    {
        isClosing = true;
        messenger.UnregisterAll(this);
        StopObservingNavigationChanges();
        navigationItems.Clear();
        navigationPath.Clear();
        Closed -= HandleClosed;
    }

    private void GoBack()
    {
        if (navigationPath.Count < 2)
        {
            return;
        }

        List<ISettingViewModel> path = [.. navigationPath.Take(navigationPath.Count - 1)];
        ISettingViewModel target = path[^1];

        if (navigationItems.TryGetValue(target, out NavigationViewItem? item))
        {
            SettingsNavigation.SelectedItem = item;
        }

        Navigate(path);
    }

    private void BuildNavigation()
    {
        if (isBuildingNavigation || isClosing)
        {
            return;
        }

        isBuildingNavigation = true;

        try
        {
            ISettingViewModel[] previousPath = [.. navigationPath];
            StopObservingNavigationChanges();
            SettingsNavigation.MenuItems.Clear();
            navigationItems.Clear();

            foreach (ISettingViewModel root in ViewModel)
            {
                SettingsNavigation.MenuItems.Add(CreateNavigationItem(root));
                ObserveNavigationChanges(root);
            }

            List<ISettingViewModel>? path = RestoreNavigationPath(previousPath);

            if (path is null)
            {
                ISettingViewModel? initial = ViewModel.FirstOrDefault();

                if (initial is null)
                {
                    return;
                }

                path = [initial];

                if (initial.Children.Count > 0)
                {
                    path.Add(initial.Children[0]);
                }
            }

            ISettingViewModel selectedItem = path.Last(navigationItems.ContainsKey);
            SettingsNavigation.SelectedItem = navigationItems[selectedItem];
            Navigate(path);
        }
        finally
        {
            isBuildingNavigation = false;
        }
    }

    private void HandleNavigationCollectionChanged(object? sender,
        NotifyCollectionChangedEventArgs args)
    {
        if (isClosing || isNavigationRebuildPending)
        {
            return;
        }

        isNavigationRebuildPending = true;

        if (!DispatcherQueue.TryEnqueue(() =>
            {
                isNavigationRebuildPending = false;
                BuildNavigation();
            }))
        {
            isNavigationRebuildPending = false;
        }
    }

    private NavigationViewItem CreateNavigationItem(ISettingViewModel viewModel)
    {
        NavigationViewItem item = new()
        {
            Content = viewModel.Title,
            IsExpanded = true,
            Margin = new Thickness(8, 0, 0, 0),
            Tag = viewModel
        };

        if (!string.IsNullOrEmpty(viewModel.Glyph))
        {
            item.Icon = new FontIcon { Glyph = viewModel.Glyph };
        }

        navigationItems[viewModel] = item;

        foreach (ISettingViewModel child in viewModel.Children)
        {
            item.MenuItems.Add(CreateNavigationItem(child));
        }

        return item;
    }

    private void ObserveNavigationChanges(ISettingViewModel viewModel)
    {
        if (viewModel is not SettingsCategoryViewModel &&
            viewModel is INotifyCollectionChanged observable &&
            !observedNavigationCollections.Contains(observable))
        {
            observable.CollectionChanged += HandleNavigationCollectionChanged;
            observedNavigationCollections.Add(observable);
        }

        foreach (ISettingViewModel child in viewModel.Children)
        {
            ObserveNavigationChanges(child);
        }
    }

    private List<ISettingViewModel>? FindNavigationPath(ISettingViewModel target)
    {
        foreach (ISettingViewModel root in ViewModel)
        {
            List<ISettingViewModel> path = [];

            if (TryFindNavigationPath(root, target, path))
            {
                return path;
            }
        }

        return null;
    }

    private List<ISettingViewModel>? RestoreNavigationPath(IReadOnlyList<ISettingViewModel> previousPath)
    {
        for (int index = previousPath.Count - 1; index >= 0; index--)
        {
            List<ISettingViewModel>? path = FindNavigationPath(previousPath[index]);

            if (path is null)
            {
                continue;
            }

            if (index < previousPath.Count - 1 &&
                path[^1].Children.Count > 0)
            {
                path.Add(path[^1].Children[0]);
            }

            return path;
        }

        return null;
    }

    private void StopObservingNavigationChanges()
    {
        foreach (INotifyCollectionChanged collection in observedNavigationCollections)
        {
            collection.CollectionChanged -= HandleNavigationCollectionChanged;
        }

        observedNavigationCollections.Clear();
    }

    private static bool TryFindNavigationPath(ISettingViewModel current,
        ISettingViewModel target,
        List<ISettingViewModel> path)
    {
        path.Add(current);

        if (ReferenceEquals(current, target))
        {
            return true;
        }

        foreach (ISettingViewModel child in current.Children)
        {
            if (TryFindNavigationPath(child, target, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private void Navigate(IReadOnlyList<ISettingViewModel> path)
    {
        if (isClosing || path.Count == 0)
        {
            return;
        }

        navigationPath.Clear();
        navigationPath.AddRange(path);

        BreadcrumbItems.Clear();

        foreach (ISettingViewModel item in path)
        {
            BreadcrumbItems.Add(item.Title);
        }

        ViewModel.NavigateTo(path[^1]);
        bool isModuleSettingsVisible = path[0] is ModulesViewModel;
        AddModuleButton.Visibility = isModuleSettingsVisible ? Visibility.Visible : Visibility.Collapsed;
        ModuleDropOverlay.Visibility = Visibility.Collapsed;

        if (!isModuleSettingsVisible)
        {
            ModuleInstallInfoBar.IsOpen = false;
        }

        bool canGoBack = path.Count > 2;
        AppTitleBar.IsBackButtonEnabled = canGoBack;
        AppTitleBar.IsBackButtonVisible = canGoBack;
    }
}
