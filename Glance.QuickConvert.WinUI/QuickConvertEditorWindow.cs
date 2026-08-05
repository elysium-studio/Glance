using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.QuickConvert.WinUI;

internal sealed partial class QuickConvertEditorWindow
{
    private readonly TaskCompletionSource<QuickConversionSelection?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IReadOnlyList<IGlanceQuickConverter> converters;
    private readonly ContentControl editorPresenter;
    private readonly TextBlock errorText;
    private readonly IReadOnlyList<GlanceStorageItem> items;
    private readonly ModuleResourceTextLocalizer<QuickConvertModule> localizer;
    private readonly ComboBox providerPicker;
    private readonly Grid root;
    private readonly Border smokeLayer;
    private readonly ContentDialog dialog;
    private readonly Window window;
    private IGlanceQuickConverterEditor? editor;
    private bool isClosed;

    private QuickConvertEditorWindow(IReadOnlyList<IGlanceQuickConverter> converters,
        IReadOnlyList<GlanceStorageItem> items,
        ModuleResourceTextLocalizer<QuickConvertModule> localizer,
        WindowId ownerWindowId)
    {
        this.converters = converters;
        this.items = items;
        this.localizer = localizer;
        DisplayArea displayArea = DisplayArea.GetFromWindowId(ownerWindowId, DisplayAreaFallback.Primary);
        providerPicker = new ComboBox
        {
            Header = localizer.GetText("Converter"),
            DisplayMemberPath = "Descriptor.DisplayName",
            ItemsSource = converters,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = converters.Count > 1 ? Visibility.Visible : Visibility.Collapsed
        };
        editorPresenter = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28)),
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        StackPanel content = new() { Spacing = 12, Width = 380 };
        content.Children.Add(new TextBlock
        {
            Text = localizer.GetText(items.Count == 1 ? "DialogOneFile" : "DialogManyFiles", items.Count),
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
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 520
        };
        dialog = new ContentDialog
        {
            Title = localizer.GetText("ConvertFiles"),
            Content = scrollViewer,
            PrimaryButtonText = localizer.GetText("Convert"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        dialog.Closing += HandleDialogClosing;
        dialog.Resources["ContentDialogSmokeFill"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        providerPicker.SelectionChanged += HandleProviderChanged;
        smokeLayer = new Border
        {
            Background = ResolveSmokeBrush(),
            IsHitTestVisible = false,
            Opacity = 0
        };
        root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)) };
        root.Children.Add(smokeLayer);
        root.Children.Add(dialog);
        root.Loaded += HandleRootLoaded;
        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        window.Closed += HandleWindowClosed;
        window.AppWindow.IsShownInSwitchers = false;
        AppWindow appWindow = window.AppWindow;
        OverlappedPresenter presenter = appWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        nint windowHandle = WindowNative.GetWindowHandle(window);
        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);
        appWindow.MoveAndResize(displayArea.OuterBounds);
        UpdateEditor();
    }

    public static Task<QuickConversionSelection?> ShowAsync(IReadOnlyList<IGlanceQuickConverter> converters,
        IReadOnlyList<GlanceStorageItem> items,
        ModuleResourceTextLocalizer<QuickConvertModule> localizer,
        WindowId ownerWindowId) => new QuickConvertEditorWindow(converters, items, localizer, ownerWindowId).ShowAsync();

    private Task<QuickConversionSelection?> ShowAsync()
    {
        window.AppWindow.Show(activateWindow: true);
        return completion.Task;
    }

    private void HandleProviderChanged(object sender,
        SelectionChangedEventArgs args) => UpdateEditor();

    private void UpdateEditor()
    {
        IGlanceQuickConverter? converter = providerPicker.SelectedItem as IGlanceQuickConverter ?? converters.FirstOrDefault();
        editor = converter?.CreateEditor(items);
        editorPresenter.Content = editor?.Content;
        errorText.Visibility = Visibility.Collapsed;
    }

    private void HandlePrimaryButtonClick(ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
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

        _ = completion.TrySetResult(new QuickConversionSelection(converter, options));
    }

    private async void HandleRootLoaded(object sender,
        RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;

        try
        {
            AnimateSmoke(1);
            dialog.XamlRoot = root.XamlRoot;
            _ = await dialog.ShowAsync(ContentDialogPlacement.InPlace);
            _ = completion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            _ = completion.TrySetException(exception);
        }
        finally
        {
            Close();
        }
    }

    private void HandleDialogClosing(ContentDialog sender,
        ContentDialogClosingEventArgs args) => AnimateSmoke(0);

    private void HandleWindowClosed(object sender,
        WindowEventArgs args)
    {
        isClosed = true;
        _ = completion.TrySetResult(null);
    }

    private void AnimateSmoke(double opacity)
    {
        DoubleAnimation animation = new()
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(83)
        };
        Storyboard.SetTarget(animation, smokeLayer);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
        Storyboard storyboard = new();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static Brush ResolveSmokeBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Windows.UI.Color.FromArgb(77, 0, 0, 0));

    private void Close()
    {
        if (isClosed)
        {
            return;
        }

        isClosed = true;
        dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
        dialog.Closing -= HandleDialogClosing;
        providerPicker.SelectionChanged -= HandleProviderChanged;
        window.Closed -= HandleWindowClosed;
        window.Close();
    }
}
