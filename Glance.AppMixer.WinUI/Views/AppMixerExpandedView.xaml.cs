using Glance.AppMixer;
using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glance.AppMixer.WinUI;

public sealed partial class AppMixerExpandedView :
    UserControl
{
    private readonly ModuleResourceTextLocalizer<AppMixerModule> localizer;

    public AppMixerExpandedView(AppMixerViewModel viewModel,
        ModuleResourceTextLocalizer<AppMixerModule> localizer)
    {
        ViewModel = viewModel;
        this.localizer = localizer;
        InitializeComponent();
    }

    public AppMixerViewModel ViewModel { get; }

    public FrameworkElement ConnectedAnimationElement => StatusIndicator;

    public string Title => localizer.GetText("ModuleDisplayName");

    private string ToUpper(string value) => value.ToUpperInvariant();

    private void HandleFlipViewLoaded(object sender,
        RoutedEventArgs args)
    {
        if (FindDescendant<Button>(ApplicationFlipView, "PART_PreviousButton") is Button previousButton)
        {
            Canvas.SetZIndex(previousButton, 1);
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent,
        string name) where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            if (child is T element && element.Name == name)
            {
                return element;
            }

            if (FindDescendant<T>(child, name) is T descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private Visibility WhenEmpty(bool hasApplications) => hasApplications ? Visibility.Collapsed : Visibility.Visible;

    private Visibility WhenPopulated(bool hasApplications) => hasApplications ? Visibility.Visible : Visibility.Collapsed;
}
