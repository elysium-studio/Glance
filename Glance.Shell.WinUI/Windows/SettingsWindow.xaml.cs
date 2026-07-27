using Glance.Application.Abstractions;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using Windows.Graphics;

namespace Glance.Shell.WinUI;

public sealed partial class SettingsWindow :
    Window
{
    private const int WindowWidth = 1100;
    private const int WindowHeight = 680;
    private readonly ITextLocalizer localizer;
    private readonly ModuleSettingsNavigationService moduleNavigation;

    public SettingsWindow(ModuleSettingsNavigationService moduleNavigation,
        ITextLocalizer localizer)
    {
        this.moduleNavigation = moduleNavigation;
        this.localizer = localizer;
        InitializeComponent();

        moduleNavigation.CurrentModuleChanged += HandleCurrentModuleChanged;
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

    private void HandleLoaded(object sender,
        RoutedEventArgs args) =>
        UpdateNavigation(ViewModel.SelectedItem);

    private void HandleNavigationSelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        moduleNavigation.GoBack();
        UpdateNavigation(args.SelectedItem as ISettingViewModel);
    }

    private void HandleCurrentModuleChanged(object? sender,
        EventArgs args) =>
        UpdateNavigation(ViewModel.SelectedItem);

    private void HandleBackRequested(TitleBar sender,
        object args) =>
        moduleNavigation.GoBack();

    private void HandleBreadcrumbItemClicked(BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0 &&
            BreadcrumbItems.Count > 1)
        {
            moduleNavigation.GoBack();
        }
    }

    private void HandleClosed(object sender,
        WindowEventArgs args)
    {
        moduleNavigation.CurrentModuleChanged -= HandleCurrentModuleChanged;
        moduleNavigation.GoBack();
        Closed -= HandleClosed;
    }

    private void UpdateNavigation(ISettingViewModel? selectedItem)
    {
        string pageTitle = selectedItem switch
        {
            GlanceViewModel => localizer.GetText("GlanceSectionTitle.Text"),
            ModulesViewModel => localizer.GetText("ModulesSectionTitle.Text"),
            WindowsViewModel => localizer.GetText("WindowsSectionTitle.Text"),
            _ => string.Empty
        };
        ModuleSettingsItemViewModel? module = selectedItem is ModulesViewModel
            ? moduleNavigation.CurrentModule
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
        OverviewContent.Visibility = showModuleSettings ? Visibility.Collapsed : Visibility.Visible;
        ModuleSettingsContent.DataContext = module;
        ModuleSettingsContent.Visibility = showModuleSettings ? Visibility.Visible : Visibility.Collapsed;
        AppTitleBar.IsBackButtonEnabled = showModuleSettings;
        AppTitleBar.IsBackButtonVisible = showModuleSettings;
    }
}
