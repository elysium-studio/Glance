using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
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

namespace Glance.Torrents.WinUI;

internal sealed record TorrentConfirmationResult(TorrentMetadataSession Session, IReadOnlyList<string> SelectedFiles);

internal sealed class TorrentConfirmationWindow
{
    private readonly TaskCompletionSource<TorrentConfirmationResult?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource cancellation = new();
    private readonly TorrentAddCoordinator coordinator;
    private readonly TorrentInput input;
    private readonly string downloadPath;
    private readonly ModuleResourceTextLocalizer<TorrentModule> localizer;
    private readonly StackPanel details = new() { Spacing = 12, Width = 480 };
    private readonly ProgressRing progress = new() { IsActive = true, Width = 24, Height = 24 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock selectedSize = new() { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly ContentDialog dialog;
    private readonly Border smokeLayer;
    private readonly Grid root;
    private readonly Window window;
    private TorrentConfirmationViewModel? viewModel;
    private TorrentMetadataSession? session;
    private bool confirmed;
    private bool closed;

    private TorrentConfirmationWindow(TorrentAddCoordinator coordinator, TorrentInput input, string downloadPath, ModuleResourceTextLocalizer<TorrentModule> localizer, WindowId ownerWindowId)
    {
        this.coordinator = coordinator;
        this.input = input;
        this.downloadPath = downloadPath;
        this.localizer = localizer;
        StackPanel loading = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
        loading.Children.Add(progress);
        loading.Children.Add(status);
        details.Children.Add(loading);
        status.Text = input.Kind == TorrentInputKind.MagnetLink ? localizer.GetText("MetadataLoading") : localizer.GetText("TorrentReading");
        dialog = new ContentDialog
        {
            Title = localizer.GetText("ConfirmTitle"),
            Content = new ScrollViewer { Content = details, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = localizer.GetText("AddDownload"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        dialog.Resources["ContentDialogSmokeFill"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        smokeLayer = new Border { Background = ResolveSmokeBrush(), IsHitTestVisible = false, Opacity = 0 };
        root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)) };
        root.Children.Add(smokeLayer);
        root.Children.Add(dialog);
        root.Loaded += HandleRootLoaded;
        window = new Window { Content = root, ExtendsContentIntoTitleBar = true, SystemBackdrop = new TransparentTintBackdrop() };
        window.SetTitleBar(null);
        window.Closed += HandleWindowClosed;
        DisplayArea area = DisplayArea.GetFromWindowId(ownerWindowId, DisplayAreaFallback.Primary);
        window.AppWindow.IsShownInSwitchers = false;
        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        nint handle = WindowNative.GetWindowHandle(window);
        PlatformWindowExtensions.SetBorderless(handle, true);
        PlatformWindowExtensions.SetCornerRadius(handle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(handle, true);
        window.AppWindow.MoveAndResize(area.OuterBounds);
    }

    public static Task<TorrentConfirmationResult?> ShowAsync(TorrentAddCoordinator coordinator, TorrentInput input, string downloadPath, ModuleResourceTextLocalizer<TorrentModule> localizer, WindowId ownerWindowId)
        => new TorrentConfirmationWindow(coordinator, input, downloadPath, localizer, ownerWindowId).ShowAsync();

    private Task<TorrentConfirmationResult?> ShowAsync()
    {
        window.AppWindow.Show(true);
        return completion.Task;
    }

    private async void HandleRootLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;
        AnimateSmoke(1);
        dialog.XamlRoot = root.XamlRoot;
        _ = LoadMetadataAsync();
        try
        {
            _ = await dialog.ShowAsync(ContentDialogPlacement.InPlace);
            if (!confirmed) _ = completion.TrySetResult(null);
        }
        catch (Exception exception) { _ = completion.TrySetException(exception); }
        finally { await CloseAsync(); }
    }

    private async Task LoadMetadataAsync()
    {
        try
        {
            TimeSpan timeout = input.Kind == TorrentInputKind.TorrentFile
                ? TimeSpan.FromSeconds(5)
                : TimeSpan.FromSeconds(45);
            session = await coordinator.PrepareAsync(input,
                downloadPath,
                timeout,
                cancellation.Token);
            viewModel = new TorrentConfirmationViewModel();
            viewModel.Load(session);
            RenderMetadata(viewModel);
            dialog.IsPrimaryButtonEnabled = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            progress.IsActive = false;
            progress.Visibility = Visibility.Collapsed;
            status.Text = exception.Message;
            status.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28));
        }
    }

    private void RenderMetadata(TorrentConfirmationViewModel model)
    {
        details.Children.Clear();
        details.Children.Add(new TextBlock { Text = model.Name, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = $"{model.SourceType} · {FormatSize(model.TotalSize)} · {model.Files.Count} {(model.Files.Count == 1 ? "file" : "files")}", Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] });
        details.Children.Add(new TextBlock { Text = $"Download to\n{model.DownloadPath}", TextWrapping = TextWrapping.Wrap });
        StackPanel files = new() { Spacing = 4 };
        foreach (TorrentFileSelectionViewModel file in model.Files)
        {
            CheckBox check = new() { Content = $"{file.Path}  ({FormatSize(file.Size)})", IsChecked = file.IsSelected, Tag = file, HorizontalAlignment = HorizontalAlignment.Stretch };
            check.Checked += SelectionChanged;
            check.Unchecked += SelectionChanged;
            files.Children.Add(check);
        }
        details.Children.Add(new TextBlock { Text = localizer.GetText("Files"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        details.Children.Add(files);
        details.Children.Add(selectedSize);
        UpdateSelectedSize();
        if (model.Trackers.Count > 0)
        {
            details.Children.Add(new TextBlock { Text = $"{model.Trackers.Count} {(model.Trackers.Count == 1 ? "tracker" : "trackers")}", Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"] });
        }
    }

    private void SelectionChanged(object sender, RoutedEventArgs args)
    {
        if (((CheckBox)sender).Tag is TorrentFileSelectionViewModel file) file.IsSelected = ((CheckBox)sender).IsChecked == true;
        UpdateSelectedSize();
    }

    private void UpdateSelectedSize()
    {
        if (viewModel is null) return;
        selectedSize.Text = $"{localizer.GetText("SelectedSize")}: {FormatSize(viewModel.SelectedSize)}";
        dialog.IsPrimaryButtonEnabled = viewModel.GetSelectedFiles().Count > 0;
    }

    private void HandlePrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (session is null || viewModel is null || viewModel.GetSelectedFiles().Count == 0) { args.Cancel = true; return; }
        confirmed = true;
        _ = completion.TrySetResult(new TorrentConfirmationResult(session, viewModel.GetSelectedFiles()));
    }

    private void HandleWindowClosed(object sender, WindowEventArgs args)
    {
        closed = true;
        cancellation.Cancel();
        _ = completion.TrySetResult(null);
    }

    private async Task CloseAsync()
    {
        cancellation.Cancel();
        if (!confirmed && session is not null) await coordinator.CancelAsync(session);
        AnimateSmoke(0);
        if (!closed)
        {
            closed = true;
            dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
            window.Closed -= HandleWindowClosed;
            window.Close();
        }
        cancellation.Dispose();
    }

    private void AnimateSmoke(double opacity)
    {
        DoubleAnimation animation = new() { To = opacity, Duration = TimeSpan.FromMilliseconds(83) };
        Storyboard.SetTarget(animation, smokeLayer);
        Storyboard.SetTargetProperty(animation, nameof(UIElement.Opacity));
        Storyboard storyboard = new(); storyboard.Children.Add(animation); storyboard.Begin();
    }

    private static Brush ResolveSmokeBrush() => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SmokeFillColorDefaultBrush", out object value) && value is Brush brush ? brush : new SolidColorBrush(Windows.UI.Color.FromArgb(77, 0, 0, 0));
    private static string FormatSize(long bytes) => bytes switch { >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.0} GB", >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.0} MB", >= 1024 => $"{bytes / 1024d:0} KB", _ => $"{bytes} B" };
}
