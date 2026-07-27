using CommunityToolkit.Mvvm.Messaging;
using Glance.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsWindow :
    Window,
    IRecipient<ModuleSettingsNavigationRequestedEventArgs>,
    IRecipient<ModuleReorderingRequestedEventArgs>
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 680;
    private readonly ITextLocalizer localizer;
    private readonly ILogger<SettingsWindow> logger;
    private readonly IMessenger messenger;
    private ModuleSettingsItemViewModel? currentModule;

    public SettingsWindow(IMessenger messenger,
        ITextLocalizer localizer,
        ILogger<SettingsWindow> logger)
    {
        this.messenger = messenger;
        this.localizer = localizer;
        this.logger = logger;
        InitializeComponent();

        messenger.Register<ModuleSettingsNavigationRequestedEventArgs>(this);
        messenger.Register<ModuleReorderingRequestedEventArgs>(this);
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

    public SettingsViewModel ViewModel => (SettingsViewModel)((FrameworkElement)Content).DataContext;

    public Visibility ToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    public void Receive(ModuleSettingsNavigationRequestedEventArgs message)
    {
        if (!message.Module.CanExpand ||
            ReferenceEquals(currentModule, message.Module))
        {
            return;
        }

        currentModule = message.Module;
        UpdateNavigation(ViewModel.SelectedItem);
    }

    public void Receive(ModuleReorderingRequestedEventArgs message) =>
        ViewModel.BeginReordering();

    private void HandleLoaded(object sender,
        RoutedEventArgs args) =>
        UpdateNavigation(ViewModel.SelectedItem);

    private void HandleNavigationSelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        currentModule = null;
        UpdateNavigation(args.SelectedItem as ISettingViewModel);
    }

    private void HandleBackRequested(TitleBar sender,
        object args) =>
        GoBack();

    private async void HandleCompleteReordering(object sender,
        RoutedEventArgs args)
    {
        try
        {
            await ViewModel.CompleteReorderingAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to save module order");
        }
    }

    private void HandleCancelReordering(object sender,
        RoutedEventArgs args) =>
        ViewModel.CancelReordering();

    private void HandleBreadcrumbItemClicked(BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0 &&
            BreadcrumbItems.Count > 1)
        {
            GoBack();
        }
    }

    private void HandleClosed(object sender,
        WindowEventArgs args)
    {
        ViewModel.CancelReordering();
        messenger.UnregisterAll(this);
        currentModule = null;
        Closed -= HandleClosed;
    }

    private void GoBack()
    {
        if (ViewModel.IsReorderingCurrentView)
        {
            ViewModel.CancelReordering();
            return;
        }

        if (currentModule is null)
        {
            return;
        }

        currentModule = null;
        UpdateNavigation(ViewModel.SelectedItem);
    }

    private void UpdateNavigation(ISettingViewModel? selectedItem)
    {
        string pageTitle = selectedItem switch
        {
            GlanceViewModel => localizer.GetText("GlanceSectionTitle/Text"),
            ModulesViewModel => localizer.GetText("ModulesSectionTitle/Text"),
            WindowsViewModel => localizer.GetText("WindowsSectionTitle/Text"),
            _ => string.Empty
        };
        ModuleSettingsItemViewModel? module = selectedItem is ModulesViewModel
            ? currentModule
            : null;

        BreadcrumbItems.Clear();

        if (!string.IsNullOrEmpty(pageTitle))
        {
            BreadcrumbItems.Add(pageTitle);
        }

        if (module is not null)
        {
            BreadcrumbItems.Add(module.DisplayName);
        }

        bool showModuleSettings = module is not null;
        ViewModel.NavigateTo(module?.Settings ?? selectedItem);
        AppTitleBar.IsBackButtonEnabled = showModuleSettings;
        AppTitleBar.IsBackButtonVisible = showModuleSettings;
    }
}
