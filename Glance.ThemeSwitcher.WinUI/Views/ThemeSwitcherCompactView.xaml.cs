using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class ThemeSwitcherCompactView :
    UserControl
{
    public ThemeSwitcherCompactView(ThemeSwitcherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public ThemeSwitcherViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    private void HandleLoaded(object sender, RoutedEventArgs args) => ViewModel.PropertyChanged += HandlePropertyChanged;

    private void HandleUnloaded(object sender, RoutedEventArgs args) => ViewModel.PropertyChanged -= HandlePropertyChanged;

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ThemeSwitcherViewModel.EffectiveTheme))
        {
            ThemeSwitcherMotion.Play(StatusIndicator);
        }
    }
}
