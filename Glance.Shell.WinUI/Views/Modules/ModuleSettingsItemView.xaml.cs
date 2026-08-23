using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;

namespace Glance.Shell.WinUI;

public sealed partial class ModuleSettingsItemView :
    UserControl
{
    private ModuleSettingsItemViewModel? subscribedViewModel;

    public ModuleSettingsItemView()
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
    }

    public ModuleSettingsItemViewModel ViewModel => (ModuleSettingsItemViewModel)DataContext;

    public Thickness GetActionMargin(bool canExpand, bool showInstallAction, bool showUpdateAction) => canExpand || showInstallAction || showUpdateAction ? new() : new(0, 0, 28, 0);

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged += HandleActualThemeChanged;
        SubscribeToViewModel();
        ApplyHeaderIcon();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged -= HandleActualThemeChanged;
        UnsubscribeFromViewModel();
    }

    private void HandleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        UnsubscribeFromViewModel();

        if (args.NewValue is not ModuleSettingsItemViewModel)
        {
            return;
        }

        Bindings.Update();

        if (!IsLoaded)
        {
            return;
        }

        SubscribeToViewModel();
        ApplyHeaderIcon();
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyHeaderIcon();

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ModuleSettingsItemViewModel.FeedIcon) or nameof(ModuleSettingsItemViewModel.IconGlyph) or nameof(ModuleSettingsItemViewModel.IconFontFamily))
        {
            ApplyHeaderIcon();
        }
    }

    private void ApplyHeaderIcon()
    {
        if (ViewModel.CreateIcon(ActualTheme == ElementTheme.Light) is IconElement icon)
        {
            SettingsCard.HeaderIcon = icon;
            return;
        }

        Brush accent = GlanceModuleFeedIconFactory.CreateAccentBrush(ViewModel.FeedIcon, ActualTheme == ElementTheme.Light) ?? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
        SettingsCard.HeaderIcon = GlanceModuleFeedIconFactory.Create(ViewModel.FeedIcon, ActualTheme == ElementTheme.Light, accent, 20) ?? new FontIcon
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(ViewModel.IconFontFamily),
            Foreground = accent,
            Glyph = ViewModel.IconGlyph
        };
    }

    private void SubscribeToViewModel()
    {
        subscribedViewModel = DataContext as ModuleSettingsItemViewModel;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }
    }

    private void UnsubscribeFromViewModel()
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            subscribedViewModel = null;
        }
    }

    private async void HandleInstallClicked(object sender, RoutedEventArgs args) => _ = await ViewModel.InstallAsync();

    private async void HandleUninstallClicked(object sender, RoutedEventArgs args)
    {
        if (!ViewModel.CanUninstall || XamlRoot is null)
        {
            return;
        }

        if (await ViewModel.ConfirmUninstallAsync(XamlRoot))
        {
            _ = await ViewModel.UninstallAsync();
        }
    }
}
