using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glance.QuickConvert.WinUI;

internal sealed partial class QuickConvertEditorWindow
{
    private readonly IReadOnlyList<IGlanceQuickConverter> converters;
    private readonly GlanceContentContext context;
    private readonly ContentDialogWindow dialog;
    private readonly ContentControl editorPresenter;
    private readonly TextBlock errorText;
    private readonly ModuleResourceTextLocalizer<QuickConvertModule> localizer;
    private readonly WindowId ownerWindowId;
    private readonly ComboBox providerPicker;
    private IGlanceQuickConverterEditor? editor;
    private QuickConversionSelection? selection;

    private QuickConvertEditorWindow(IReadOnlyList<IGlanceQuickConverter> converters, GlanceContentContext context, ModuleResourceTextLocalizer<QuickConvertModule> localizer, WindowId ownerWindowId)
    {
        this.converters = converters;
        this.context = context;
        this.localizer = localizer;
        this.ownerWindowId = ownerWindowId;

        providerPicker = new ComboBox
        {
            Header = localizer.GetText("Converter"),
            DisplayMemberPath = "Descriptor.DisplayName",
            ItemsSource = converters,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = converters.Count > 1 ? Visibility.Visible : Visibility.Collapsed
        };
        editorPresenter = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28)),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        StackPanel content = new() { Spacing = 12, Width = 380 };
        content.Children.Add(new TextBlock
        {
            Text = CreatePrompt(context, localizer),
            TextWrapping = TextWrapping.Wrap,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["BodyTextBlockStyle"] as Style
        });
        content.Children.Add(providerPicker);
        content.Children.Add(editorPresenter);
        content.Children.Add(errorText);

        ScrollViewer scrollViewer = new()
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        dialog = new ContentDialogWindow
        {
            Width = 428,
            Height = 460,
            Title = localizer.GetText("ConvertFiles"),
            Content = scrollViewer,
            PrimaryButtonText = localizer.GetText("Convert"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        providerPicker.SelectionChanged += HandleProviderChanged;
        UpdateEditor();
    }

    public static Task<QuickConversionSelection?> ShowAsync(IReadOnlyList<IGlanceQuickConverter> converters, GlanceContentContext context, ModuleResourceTextLocalizer<QuickConvertModule> localizer, WindowId ownerWindowId) => new QuickConvertEditorWindow(converters, context, localizer, ownerWindowId).ShowAsync();

    private async Task<QuickConversionSelection?> ShowAsync()
    {
        try
        {
            ContentDialogResult result = await dialog.ShowAsync(ownerWindowId);
            return result == ContentDialogResult.Primary ? selection : null;
        }
        finally
        {
            dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
            providerPicker.SelectionChanged -= HandleProviderChanged;
        }
    }

    private void HandleProviderChanged(object sender, SelectionChangedEventArgs args) => UpdateEditor();

    private void UpdateEditor()
    {
        IGlanceQuickConverter? converter = providerPicker.SelectedItem as IGlanceQuickConverter ?? converters.FirstOrDefault();
        editor = converter?.CreateEditor(context);
        editorPresenter.Content = editor?.Content;
        errorText.Visibility = Visibility.Collapsed;
    }

    private void HandlePrimaryButtonClick(object? sender, ContentDialogWindowButtonClickEventArgs args)
    {
        IGlanceQuickConverter? converter = providerPicker.SelectedItem as IGlanceQuickConverter ?? converters.FirstOrDefault();
        object? options = null;
        string? errorMessage = null;

        if (converter is null || (editor is not null && !editor.TryCreateOptions(out options, out errorMessage)))
        {
            errorText.Text = errorMessage ?? localizer.GetText("ConversionOptionsInvalid");
            errorText.Visibility = Visibility.Visible;
            args.Cancel = true;
            return;
        }

        selection = new QuickConversionSelection(converter, options);
    }

    private static string CreatePrompt(GlanceContentContext context, ModuleResourceTextLocalizer<QuickConvertModule> localizer) => context.Kind == GlanceContentKind.FilesAndFolders ? localizer.GetText(context.StorageItems.Count == 1 ? "DialogOneFile" : "DialogManyFiles", context.StorageItems.Count) : localizer.GetText("DialogLink");
}
