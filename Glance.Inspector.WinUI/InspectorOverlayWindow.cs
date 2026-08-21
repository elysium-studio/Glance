using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glance.Inspector.WinUI;

internal sealed class InspectorOverlayWindow
{
    private readonly ContentDialogWindow dialog;
    private readonly ModuleResourceTextLocalizer<InspectorModule> localizer;
    private readonly WindowId ownerWindowId;
    private readonly InfoBar status;

    private InspectorOverlayWindow(IReadOnlyList<GlanceInspectionSection> sections, IReadOnlyList<IGlanceInspectionAction> actions, ModuleResourceTextLocalizer<InspectorModule> localizer, WindowId ownerWindowId)
    {
        this.localizer = localizer;
        this.ownerWindowId = ownerWindowId;

        StackPanel content = new() { Spacing = 12, Width = 572 };

        foreach (GlanceInspectionSection section in sections)
        {
            content.Children.Add(CreateSection(section));
        }

        if (actions.Count > 0)
        {
            content.Children.Add(CreateActions(actions));
        }

        status = new InfoBar { IsClosable = false, IsOpen = false };
        content.Children.Add(status);

        ScrollViewer scrollViewer = new()
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        dialog = new ContentDialogWindow
        {
            Width = 620,
            Height = 756,
            Content = scrollViewer,
            CloseButtonText = localizer.GetText("Close"),
            DefaultButton = ContentDialogButton.Close
        };
    }

    public static Task ShowAsync(IReadOnlyList<GlanceInspectionSection> sections, IReadOnlyList<IGlanceInspectionAction> actions, ModuleResourceTextLocalizer<InspectorModule> localizer, WindowId ownerWindowId) => new InspectorOverlayWindow(sections, actions, localizer, ownerWindowId).ShowAsync();

    private async Task ShowAsync() => _ = await dialog.ShowAsync(ownerWindowId);

    private Border CreateSection(GlanceInspectionSection section)
    {
        StackPanel content = new() { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = section.Title, Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"] as Style });

        foreach (GlanceInspectionProperty property in section.Properties)
        {
            Grid row = new() { ColumnSpacing = 16 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock label = new() { Text = property.Label, Foreground = ResolveBrush("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap };
            TextBlock value = new() { Text = property.Value, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(value, 1);
            row.Children.Add(label);
            row.Children.Add(value);
            content.Children.Add(row);
        }

        if (section.Distribution is not null)
        {
            content.Children.Add(new InspectionDistributionView(section.Distribution));
        }

        return new Border { Background = ResolveBrush("CardBackgroundFillColorDefaultBrush"), BorderBrush = ResolveBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Child = content };
    }

    private StackPanel CreateActions(IReadOnlyList<IGlanceInspectionAction> actions)
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = localizer.GetText("Actions"), Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyStrongTextBlockStyle"] as Style });
        Grid items = new() { ColumnSpacing = 8, RowSpacing = 8 };
        items.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        items.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        items.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int index = 0; index < actions.Count; index++)
        {
            IGlanceInspectionAction action = actions[index];

            if (index % 3 == 0)
            {
                items.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            StackPanel content = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
            content.Children.Add(new FontIcon { Glyph = action.Glyph, FontSize = 14 });
            content.Children.Add(new TextBlock { Text = action.DisplayName });
            Button button = new() { Content = content, MinWidth = 132, HorizontalAlignment = HorizontalAlignment.Stretch, Tag = action };
            button.Click += HandleActionClicked;
            Grid.SetColumn(button, index % 3);
            Grid.SetRow(button, index / 3);
            items.Children.Add(button);
        }

        panel.Children.Add(items);
        return panel;
    }

    private async void HandleActionClicked(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: IGlanceInspectionAction action } button)
        {
            return;
        }

        button.IsEnabled = false;
        status.IsOpen = false;

        try
        {
            await action.ExecuteAsync();
        }
        catch
        {
            status.Message = localizer.GetText("ActionFailed");
            status.Severity = InfoBarSeverity.Error;
            status.IsOpen = true;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private static Brush ResolveBrush(string key) => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush ? brush : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
}
