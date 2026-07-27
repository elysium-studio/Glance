using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeSwitcherExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<ThemeSwitcherModule> localizer;

    public ThemeSwitcherExpandedView(ThemeSwitcherViewModel viewModel,
        ModuleResourceTextLocalizer<ThemeSwitcherModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public ThemeSwitcherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    public string LightLabel => localizer.GetText("LightLabel");

    public string SunsetLabel => localizer.GetText("SunsetLabel");

    public string DarkLabel => localizer.GetText("DarkLabel");

    private Brush LightBackground(ThemePreference preference) => GetBackground(preference == ThemePreference.Light);

    private Brush SunsetBackground(ThemePreference preference) => GetBackground(preference == ThemePreference.Sunset);

    private Brush DarkBackground(ThemePreference preference) => GetBackground(preference == ThemePreference.Dark);

    private Brush LightForeground(ThemePreference preference) => GetForeground(preference == ThemePreference.Light);

    private Brush SunsetForeground(ThemePreference preference) => GetForeground(preference == ThemePreference.Sunset);

    private Brush DarkForeground(ThemePreference preference) => GetForeground(preference == ThemePreference.Dark);

    private Brush GetBackground(bool selected) => selected
        ? (Brush)Resources["GlanceThemeSwitcherIconBrush"]
        : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    private Brush GetForeground(bool selected) => selected
        ? (Brush)Resources["GlanceThemeSwitcherSelectedForegroundBrush"]
        : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"];

    private bool IsActionEnabled(bool isBusy) => !isBusy;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private void HandleLoaded(object sender, RoutedEventArgs args) =>
        ViewModel.PropertyChanged += HandlePropertyChanged;

    private void HandleUnloaded(object sender, RoutedEventArgs args) =>
        ViewModel.PropertyChanged -= HandlePropertyChanged;

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ThemeSwitcherViewModel.EffectiveTheme))
        {
            ThemeSwitcherMotion.Play(StatusIndicator);
        }
    }
}
