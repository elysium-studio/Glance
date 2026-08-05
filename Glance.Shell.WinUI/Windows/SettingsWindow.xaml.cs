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
using System.Linq;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsWindow :
    Window,
    IRecipient<SettingsNavigationRequestedEventArgs>
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 680;
    private readonly IApplicationLifetime applicationLifetime;
    private readonly ITextLocalizer localizer;
    private readonly IMessenger messenger;
    private readonly Dictionary<ISettingViewModel, NavigationViewItem> navigationItems = [];
    private readonly List<ISettingViewModel> navigationPath = [];
    private readonly List<INotifyCollectionChanged> observedNavigationCollections = [];
    private bool isBuildingNavigation;
    private bool isClosing;
    private bool isQuitDialogOpen;

    public SettingsWindow(IMessenger messenger,
        ITextLocalizer localizer,
        IApplicationLifetime applicationLifetime)
    {
        this.messenger = messenger;
        this.localizer = localizer;
        this.applicationLifetime = applicationLifetime;
        InitializeComponent();

        messenger.Register<SettingsNavigationRequestedEventArgs>(this);
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
        if (!isClosing)
        {
            BuildNavigation();
        }
    }

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

    private async void HandleSettingItemsReordered(ListViewBase sender,
        DragItemsCompletedEventArgs args)
    {
        if (sender.ItemsSource is ISettingViewModel viewModel)
        {
            await viewModel.SaveOrderAsync();
        }
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
            ContentDialog dialog = new()
            {
                XamlRoot = ((FrameworkElement)Content).XamlRoot,
                Title = localizer.GetText("QuitDialogTitle"),
                Content = localizer.GetText("QuitDialogMessage"),
                PrimaryButtonText = localizer.GetText("QuitDialogPrimaryButton"),
                CloseButtonText = localizer.GetText("QuitDialogCloseButton"),
                DefaultButton = ContentDialogButton.Close
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

                if (root is INotifyCollectionChanged observable)
                {
                    observable.CollectionChanged += HandleNavigationCollectionChanged;
                    observedNavigationCollections.Add(observable);
                }
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
        if (DispatcherQueue.HasThreadAccess)
        {
            BuildNavigation();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(BuildNavigation);
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

            path.AddRange(previousPath.Skip(index + 1));
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
        bool canGoBack = path.Count > 2;
        AppTitleBar.IsBackButtonEnabled = canGoBack;
        AppTitleBar.IsBackButtonVisible = canGoBack;
    }
}
