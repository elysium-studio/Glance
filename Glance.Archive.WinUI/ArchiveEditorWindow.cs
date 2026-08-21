using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glance.Archive.WinUI;

internal sealed class ArchiveEditorWindow
{
    private readonly ComboBox compressionPicker;
    private readonly ContentDialogWindow dialog;
    private readonly ComboBox formatPicker;
    private readonly bool containsOnlyArchives;
    private readonly ModuleResourceTextLocalizer<ArchiveModule> localizer;
    private readonly ComboBox operationPicker;
    private ArchiveSelection? selection;

    private ArchiveEditorWindow(GlanceContentContext context, ModuleResourceTextLocalizer<ArchiveModule> localizer)
    {
        this.localizer = localizer;
        containsOnlyArchives = context.StorageItems.All(item => !item.IsFolder && ArchiveFile.IsArchive(item.Path));
        operationPicker = new ComboBox
        {
            Header = localizer.GetText("ArchiveAction"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = containsOnlyArchives ? new[] { localizer.GetText("ExtractArchive"), localizer.GetText("ConvertArchive") } : new[] { localizer.GetText("CreateArchive") },
            SelectedIndex = 0,
            Visibility = containsOnlyArchives ? Visibility.Visible : Visibility.Collapsed
        };
        formatPicker = new ComboBox
        {
            Header = localizer.GetText("ArchiveFormat"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "ZIP", "7Z", "TAR", "TAR.GZ" },
            SelectedIndex = 0,
            Visibility = containsOnlyArchives ? Visibility.Collapsed : Visibility.Visible
        };
        compressionPicker = new ComboBox
        {
            Header = localizer.GetText("Compression"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { localizer.GetText("FastCompression"), localizer.GetText("BalancedCompression"), localizer.GetText("SmallestArchive") },
            SelectedIndex = 1,
            Visibility = containsOnlyArchives ? Visibility.Collapsed : Visibility.Visible
        };
        StackPanel content = new() { Spacing = 12, Width = 380 };
        content.Children.Add(new TextBlock
        {
            Text = localizer.GetText(containsOnlyArchives ? context.StorageItems.Count == 1 ? "ArchiveDialogOneArchive" : "ArchiveDialogManyArchives" : context.StorageItems.Count == 1 ? "ArchiveDialogOneItem" : "ArchiveDialogManyItems", context.StorageItems.Count),
            TextWrapping = TextWrapping.Wrap,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyTextBlockStyle"] as Style
        });
        content.Children.Add(operationPicker);
        content.Children.Add(formatPicker);
        content.Children.Add(compressionPicker);
        dialog = new ContentDialogWindow
        {
            Width = 428,
            Height = 380,
            Title = localizer.GetText("ModuleDisplayName"),
            Content = content,
            PrimaryButtonText = localizer.GetText(containsOnlyArchives ? "Extract" : "Create"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        operationPicker.SelectionChanged += HandleOperationChanged;
        formatPicker.SelectionChanged += HandleFormatChanged;
        UpdateOptionsVisibility();
    }

    public static Task<ArchiveSelection?> ShowAsync(GlanceContentContext context, ModuleResourceTextLocalizer<ArchiveModule> localizer, WindowId ownerWindowId) => new ArchiveEditorWindow(context, localizer).ShowAsync(ownerWindowId);

    private async Task<ArchiveSelection?> ShowAsync(WindowId ownerWindowId)
    {
        try
        {
            ContentDialogResult result = await dialog.ShowAsync(ownerWindowId);
            return result == ContentDialogResult.Primary ? selection : null;
        }
        finally
        {
            dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
            operationPicker.SelectionChanged -= HandleOperationChanged;
            formatPicker.SelectionChanged -= HandleFormatChanged;
        }
    }

    private void HandleOperationChanged(object sender, SelectionChangedEventArgs args)
    {
        dialog.PrimaryButtonText = localizer.GetText(containsOnlyArchives && operationPicker.SelectedIndex == 0 ? "Extract" : containsOnlyArchives ? "Convert" : "Create");
        UpdateOptionsVisibility();
    }

    private void HandleFormatChanged(object sender, SelectionChangedEventArgs args) => UpdateOptionsVisibility();

    private void HandlePrimaryButtonClick(object? sender, ContentDialogWindowButtonClickEventArgs args)
    {
        ArchiveOperation operation = containsOnlyArchives ? operationPicker.SelectedIndex == 0 ? ArchiveOperation.Extract : ArchiveOperation.Convert : ArchiveOperation.Create;
        ArchiveFormat format = (ArchiveFormat)Math.Max(0, formatPicker.SelectedIndex);
        ArchiveCompressionLevel compressionLevel = (ArchiveCompressionLevel)Math.Max(0, compressionPicker.SelectedIndex);
        selection = new ArchiveSelection(new ArchiveOperationOptions(operation, format, compressionLevel));
    }

    private void UpdateOptionsVisibility()
    {
        bool showArchiveOptions = !containsOnlyArchives || operationPicker.SelectedIndex == 1;
        bool showCompression = showArchiveOptions && formatPicker.SelectedIndex is 0 or 3;
        formatPicker.Visibility = showArchiveOptions ? Visibility.Visible : Visibility.Collapsed;
        compressionPicker.Visibility = showCompression ? Visibility.Visible : Visibility.Collapsed;
    }
}
