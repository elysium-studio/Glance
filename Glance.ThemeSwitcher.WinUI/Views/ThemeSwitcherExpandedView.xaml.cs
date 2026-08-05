using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private bool IsLight(ThemePreference preference) => preference == ThemePreference.Light;

    private bool IsSunset(ThemePreference preference) => preference == ThemePreference.Sunset;

    private bool IsDark(ThemePreference preference) => preference == ThemePreference.Dark;

    private void SelectLight()
    {
        UpdateSelection(ThemePreference.Light);
        _ = ViewModel.SelectLightAsync();
    }

    private void SelectSunset()
    {
        UpdateSelection(ThemePreference.Sunset);
        _ = ViewModel.SelectSunsetAsync();
    }

    private void SelectDark()
    {
        UpdateSelection(ThemePreference.Dark);
        _ = ViewModel.SelectDarkAsync();
    }

    private bool IsActionEnabled(bool isBusy) => !isBusy;

    private string ToUpper(string value) => value.ToUpperInvariant();

    private void HandleLoaded(object sender, RoutedEventArgs args) => ViewModel.PropertyChanged += HandlePropertyChanged;

    private void HandleUnloaded(object sender, RoutedEventArgs args) => ViewModel.PropertyChanged -= HandlePropertyChanged;

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ThemeSwitcherViewModel.Preference) or nameof(ThemeSwitcherViewModel.ErrorText))
        {
            UpdateSelection(ViewModel.Preference);
        }

        if (args.PropertyName == nameof(ThemeSwitcherViewModel.EffectiveTheme))
        {
            ThemeSwitcherMotion.Play(StatusIndicator);
        }
    }

    private void UpdateSelection(ThemePreference preference)
    {
        LightButton.IsChecked = preference == ThemePreference.Light;
        SunsetButton.IsChecked = preference == ThemePreference.Sunset;
        DarkButton.IsChecked = preference == ThemePreference.Dark;
    }
}
