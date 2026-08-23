using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Presentation.Abstractions;
using Elysium.UI.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsWindow :
    Window,
    IRecipient<SettingsNavigationRequestedEventArgs>,
    INavigationRouteTarget
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 680;
    private readonly IApplicationLifetime applicationLifetime;
    private readonly IMessenger messenger;
    private readonly INavigator navigator;
    private readonly Dictionary<ISettingViewModel, NavigationViewItem> navigationItems = [];
    private readonly List<ISettingViewModel> navigationPath = [];
    private readonly List<INotifyCollectionChanged> observedNavigationCollections = [];
    private bool isBuildingNavigation;
    private bool isNavigationRebuildPending;
    private bool isAboutDialogOpen;
    private bool isLoaded;
    private bool isClosing;
    private bool isQuitDialogOpen;
    private NavigationRoute? pendingRoute;

    public SettingsWindow(IMessenger messenger, IApplicationLifetime applicationLifetime, INavigator navigator)
    {
        InitializeComponent();

        this.messenger = messenger;
        this.applicationLifetime = applicationLifetime;
        this.navigator = navigator;

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

    public Task HandleRouteAsync(NavigationRoute route)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyRoute(route);
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!DispatcherQueue.TryEnqueue(() =>
            {
                ApplyRoute(route);
                completion.TrySetResult();
            }))
        {
            completion.TrySetException(new InvalidOperationException("The settings dispatcher rejected the navigation route."));
        }

        return completion.Task;
    }

    public void Receive(SettingsNavigationRequestedEventArgs message)
    {
        if (isClosing || ReferenceEquals(ViewModel.CurrentView, message.Target))
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

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        if (!isClosing &&
            ((FrameworkElement)Content).DataContext is SettingsViewModel)
        {
            isLoaded = true;
            BuildNavigation();
            ApplyPendingRoute();
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

    private async void HandleQuitTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isQuitDialogOpen)
        {
            return;
        }

        isQuitDialogOpen = true;

        try
        {
            NavigationParameters parameters = CreateDialogParameters();
            NavigationDialogResult result = await navigator.NavigateAsync<NavigationDialogResult>(nameof(QuitDialog), null, parameters);

            if (result == NavigationDialogResult.Primary)
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

    private async void HandleAboutTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs args)
    {
        if (isAboutDialogOpen)
        {
            return;
        }

        isAboutDialogOpen = true;

        try
        {
            await navigator.NavigateAsync(nameof(AboutDialog), null, CreateDialogParameters());
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
        isLoaded = false;
        isClosing = true;
        messenger.UnregisterAll(this);
        StopObservingNavigationChanges();
        navigationItems.Clear();
        navigationPath.Clear();
        Closed -= HandleClosed;
    }

    private void ApplyRoute(NavigationRoute route)
    {
        if (!isLoaded || isClosing || navigationItems.Count == 0)
        {
            pendingRoute = route;
            return;
        }

        IReadOnlyList<ISettingViewModel> candidates = ViewModel.ToArray();
        List<ISettingViewModel> path = [];

        foreach (string segment in route.Segments)
        {
            ISettingViewModel? match = candidates.FirstOrDefault(candidate => string.Equals(candidate.RouteSegment, segment, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                pendingRoute = route;
                return;
            }

            path.Add(match);
            candidates = match.Children;
        }

        if (path.Count == 0)
        {
            ApplyRouteTarget(route);
            return;
        }

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
        ApplyRouteTarget(route);
    }

    private void ApplyRouteTarget(NavigationRoute route)
    {
        if (route.Target is null)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => _ = NavigationTarget.Apply((DependencyObject)Content, route));
    }

    private NavigationParameters CreateDialogParameters()
    {
        NavigationParameters parameters = new();
        parameters.Set("XamlRoot", ((FrameworkElement)Content).XamlRoot);
        return parameters;
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
                ApplyPendingRoute();
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

    private void ApplyPendingRoute()
    {
        if (pendingRoute is not NavigationRoute route)
        {
            return;
        }

        pendingRoute = null;
        ApplyRoute(route);
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
