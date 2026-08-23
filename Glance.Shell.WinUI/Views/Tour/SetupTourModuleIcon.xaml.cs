using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace Glance.Shell.WinUI;

public sealed partial class SetupTourModuleIcon :
    UserControl
{
    public SetupTourModuleIcon()
    {
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public SetupTourModuleViewModel? ViewModel { get; set; }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged += HandleActualThemeChanged;

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }

        ApplyVisuals();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ActualThemeChanged -= HandleActualThemeChanged;

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }
    }

    private void HandleActualThemeChanged(FrameworkElement sender, object args) => ApplyVisuals();

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) => ApplyVisuals();

    private void ApplyVisuals()
    {
        if (ViewModel is null)
        {
            return;
        }

        Brush accent = ResolveAccent();
        AccentPlate.Fill = accent;
        GlyphIcon.FontFamily = new FontFamily(ViewModel.GlyphFontFamily);
        GlyphIcon.Foreground = accent;
        GlyphIcon.Glyph = ViewModel.Glyph;
        object? icon = ViewModel.CreateIcon(ActualTheme == ElementTheme.Light) ?? GlanceModuleFeedIconFactory.Create(ViewModel.Icon, ActualTheme == ElementTheme.Light, accent, 20);
        CustomIconPresenter.Content = icon;
        GlyphIcon.Visibility = icon is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private Brush ResolveAccent()
    {
        if (ViewModel!.AccentResourceSource is FrameworkElement source && source.Resources.TryGetValue(ViewModel.AccentResourceKey, out object resource) && resource is Brush sourceBrush)
        {
            return sourceBrush;
        }

        if (GlanceModuleFeedIconFactory.CreateAccentBrush(ViewModel.Icon, ActualTheme == ElementTheme.Light) is Brush feedBrush)
        {
            return feedBrush;
        }

        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(ViewModel.AccentResourceKey, out object fallbackResource) && fallbackResource is Brush fallbackBrush)
        {
            return fallbackBrush;
        }

        return (Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
    }
}
