using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.Torrents.WinUI;

internal sealed record TorrentConfirmationResult(TorrentMetadataSession Session,
    IReadOnlyList<string> SelectedFiles,
    string DownloadPath);

internal sealed class TorrentConfirmationWindow
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly TaskCompletionSource<TorrentConfirmationResult?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TorrentAddCoordinator coordinator;
    private readonly Grid details = new() { Width = 600, MaxHeight = 560 };
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ContentDialog dialog;
    private string downloadPath;
    private readonly TextBlock downloadPathText = new();
    private readonly List<CheckBox> fileSelectionControls = [];
    private readonly TorrentInput input;
    private readonly ModuleResourceTextLocalizer<TorrentModule> localizer;
    private readonly ProgressRing progress = new() { IsActive = true, Width = 24, Height = 24 };
    private readonly Grid root;
    private readonly TextBlock selectedSize = new() { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly Border smokeLayer;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Window window;
    private bool closed;
    private bool confirmed;
    private bool isUpdatingSelectAll;
    private CheckBox? selectAll;
    private TorrentMetadataSession? session;
    private TorrentConfirmationViewModel? viewModel;

    private TorrentConfirmationWindow(TorrentAddCoordinator coordinator,
        TorrentInput input,
        string downloadPath,
        ModuleResourceTextLocalizer<TorrentModule> localizer,
        WindowId ownerWindowId)
    {
        this.coordinator = coordinator;
        this.input = input;
        this.downloadPath = downloadPath;
        this.localizer = localizer;

        StackPanel loading = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };
        loading.Children.Add(progress);
        loading.Children.Add(status);
        details.Children.Add(loading);
        status.Text = input.Kind == TorrentInputKind.MagnetLink
            ? localizer.GetText("MetadataLoading")
            : localizer.GetText("TorrentReading");

        dialog = new ContentDialog
        {
            Title = localizer.GetText("ConfirmTitle"),
            Content = details,
            PrimaryButtonText = localizer.GetText("AddDownload"),
            CloseButtonText = localizer.GetText("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false
        };
        dialog.PrimaryButtonClick += HandlePrimaryButtonClick;
        dialog.Opened += HandleDialogOpened;
        dialog.Resources["ContentDialogMaxWidth"] = 680d;
        dialog.Resources["ContentDialogSmokeFill"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        smokeLayer = new Border
        {
            Background = ResolveSmokeBrush(),
            IsHitTestVisible = false,
            Opacity = 0
        };
        root = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
        dispatcherQueue = root.DispatcherQueue;
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

        DisplayArea area = DisplayArea.GetFromWindowId(ownerWindowId,
            DisplayAreaFallback.Primary);
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

    public static Task<TorrentConfirmationResult?> ShowAsync(TorrentAddCoordinator coordinator,
        TorrentInput input,
        string downloadPath,
        ModuleResourceTextLocalizer<TorrentModule> localizer,
        WindowId ownerWindowId) => new TorrentConfirmationWindow(coordinator,
            input,
            downloadPath,
            localizer,
            ownerWindowId).ShowAsync();

    private Task<TorrentConfirmationResult?> ShowAsync()
    {
        window.AppWindow.Show(true);
        return completion.Task;
    }

    private async void HandleRootLoaded(object sender,
        RoutedEventArgs args)
    {
        root.Loaded -= HandleRootLoaded;
        AnimateSmoke(1);
        dialog.XamlRoot = root.XamlRoot;
        _ = LoadMetadataAsync();

        try
        {
            _ = await dialog.ShowAsync(ContentDialogPlacement.InPlace);

            if (!confirmed)
            {
                _ = completion.TrySetResult(null);
            }
        }
        catch (Exception exception)
        {
            _ = completion.TrySetException(exception);
        }
        finally
        {
            await CloseAsync();
        }
    }

    private async Task LoadMetadataAsync()
    {
        try
        {
            TorrentMetadataSession preparedSession = await coordinator.PrepareAsync(input,
                downloadPath,
                TimeSpan.FromSeconds(45),
                cancellation.Token);

            await RunOnDispatcherAsync(() =>
            {
                session = preparedSession;
                viewModel = new TorrentConfirmationViewModel();
                viewModel.Load(preparedSession);
                RenderMetadata(viewModel);
                dialog.IsPrimaryButtonEnabled = true;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            await RunOnDispatcherAsync(() =>
            {
                progress.IsActive = false;
                progress.Visibility = Visibility.Collapsed;
                status.Text = exception.Message;
                status.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 196, 43, 28));
            });
        }
    }

    private Task RunOnDispatcherAsync(Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
            }
        }))
        {
            completionSource.TrySetException(new InvalidOperationException("The Torrent confirmation overlay is unavailable."));
        }

        return completionSource.Task;
    }

    private void RenderMetadata(TorrentConfirmationViewModel model)
    {
        details.Children.Clear();
        details.RowDefinitions.Clear();
        fileSelectionControls.Clear();
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        details.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel summary = new() { Spacing = 4 };
        summary.Children.Add(new TextBlock
        {
            Text = model.Name,
            Style = ResolveStyle("BodyStrongTextBlockStyle"),
            TextWrapping = TextWrapping.Wrap
        });
        summary.Children.Add(new TextBlock
        {
            Text = $"{FormatSize(model.TotalSize)} \u00B7 {model.Files.Count} {(model.Files.Count == 1 ? "file" : "files")}",
            Foreground = ResolveBrush("TextFillColorSecondaryBrush")
        });
        Grid.SetRow(summary, 0);
        details.Children.Add(summary);

        Grid destination = new()
        {
            Margin = new Thickness(0, 16, 0, 0)
        };
        destination.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        destination.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        destination.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        destination.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock destinationLabel = new()
        {
            Text = localizer.GetText("DownloadTo"),
            Style = ResolveStyle("BodyStrongTextBlockStyle"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetColumnSpan(destinationLabel, 2);
        destination.Children.Add(destinationLabel);
        downloadPathText.Text = model.DownloadPath;
        downloadPathText.Foreground = ResolveBrush("TextFillColorSecondaryBrush");
        downloadPathText.TextTrimming = TextTrimming.CharacterEllipsis;
        downloadPathText.VerticalAlignment = VerticalAlignment.Center;
        ToolTipService.SetToolTip(downloadPathText, model.DownloadPath);
        Grid.SetRow(downloadPathText, 1);
        destination.Children.Add(downloadPathText);
        Button chooseFolderButton = new()
        {
            Content = localizer.GetText("ChooseFolder"),
            Margin = new Thickness(12, 0, 0, 0),
            MinWidth = 132
        };
        chooseFolderButton.Click += HandleChooseFolderClicked;
        Grid.SetRow(chooseFolderButton, 1);
        Grid.SetColumn(chooseFolderButton, 1);
        destination.Children.Add(chooseFolderButton);
        Grid.SetRow(destination, 1);
        details.Children.Add(destination);

        TextBlock filesTitle = new()
        {
            Text = localizer.GetText("Files"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 8)
        };
        Grid.SetRow(filesTitle, 2);
        details.Children.Add(filesTitle);

        StackPanel fileRows = new();

        foreach (TorrentFileSelectionViewModel file in model.Files)
        {
            fileRows.Children.Add(CreateFileRow(file));
        }

        Grid fileTable = new();
        fileTable.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        fileTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        fileTable.Children.Add(CreateFileHeader());
        ScrollViewer fileScroller = new()
        {
            Content = fileRows,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(fileScroller, 1);
        fileTable.Children.Add(fileScroller);

        Border tableBorder = new()
        {
            BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = fileTable,
            Height = 320
        };
        Grid.SetRow(tableBorder, 3);
        details.Children.Add(tableBorder);

        Grid footer = new()
        {
            Margin = new Thickness(0, 8, 0, 0)
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (model.Trackers.Count > 0)
        {
            footer.Children.Add(new TextBlock
            {
                Text = $"{model.Trackers.Count} {(model.Trackers.Count == 1 ? "tracker" : "trackers")}",
                Foreground = ResolveBrush("TextFillColorSecondaryBrush")
            });
        }

        selectedSize.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(selectedSize, 1);
        footer.Children.Add(selectedSize);
        Grid.SetRow(footer, 4);
        details.Children.Add(footer);
        UpdateSelectedSize();
    }

    private Grid CreateFileHeader()
    {
        Grid header = CreateFileGrid();
        header.MinHeight = 36;
        header.Background = ResolveBrush("SubtleFillColorSecondaryBrush");
        selectAll = new CheckBox
        {
            Content = string.Empty,
            IsThreeState = true,
            Width = 32,
            Height = 32,
            MinWidth = 32,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        selectAll.Click += HandleSelectAllClicked;
        header.Children.Add(selectAll);

        TextBlock nameHeader = new()
        {
            Text = localizer.GetText("FileName"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameHeader, 1);
        header.Children.Add(nameHeader);

        TextBlock sizeHeader = new()
        {
            Text = localizer.GetText("FileSize"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(sizeHeader, 2);
        header.Children.Add(sizeHeader);
        return header;
    }

    private Border CreateFileRow(TorrentFileSelectionViewModel file)
    {
        Grid row = CreateFileGrid();
        row.MinHeight = 40;
        CheckBox check = new()
        {
            Content = string.Empty,
            IsChecked = file.IsSelected,
            Tag = file,
            Width = 32,
            Height = 32,
            MinWidth = 32,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        check.Checked += SelectionChanged;
        check.Unchecked += SelectionChanged;
        fileSelectionControls.Add(check);
        row.Children.Add(check);

        TextBlock name = new()
        {
            Text = file.Path,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(name, file.Path);
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        TextBlock size = new()
        {
            Text = FormatSize(file.Size),
            Foreground = ResolveBrush("TextFillColorSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(size, 2);
        row.Children.Add(size);

        return new Border
        {
            BorderBrush = ResolveBrush("DividerStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row
        };
    }

    private static Grid CreateFileGrid()
    {
        Grid grid = new()
        {
            Padding = new Thickness(8, 0, 12, 0),
            ColumnSpacing = 8
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        return grid;
    }

    private void SelectionChanged(object sender,
        RoutedEventArgs args)
    {
        if (((CheckBox)sender).Tag is TorrentFileSelectionViewModel file)
        {
            file.IsSelected = ((CheckBox)sender).IsChecked == true;
        }

        if (!isUpdatingSelectAll)
        {
            UpdateSelectedSize();
        }
    }

    private void HandleSelectAllClicked(object sender,
        RoutedEventArgs args)
    {
        if (isUpdatingSelectAll || viewModel is null)
        {
            return;
        }

        bool isSelected = viewModel.Files.Any(file => !file.IsSelected);
        isUpdatingSelectAll = true;
        ((CheckBox)sender).IsChecked = isSelected;

        foreach (CheckBox checkBox in fileSelectionControls)
        {
            checkBox.IsChecked = isSelected;
        }

        isUpdatingSelectAll = false;
        UpdateSelectedSize();
    }

    private async void HandleChooseFolderClicked(object sender,
        RoutedEventArgs args)
    {
        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker,
            WindowNative.GetWindowHandle(window));
        StorageFolder? folder = await picker.PickSingleFolderAsync();

        if (folder is null)
        {
            return;
        }

        downloadPath = folder.Path;
        downloadPathText.Text = downloadPath;
        ToolTipService.SetToolTip(downloadPathText, downloadPath);
    }

    private void UpdateSelectedSize()
    {
        if (viewModel is null)
        {
            return;
        }

        int selectedCount = viewModel.Files.Count(file => file.IsSelected);
        selectedSize.Text = $"{localizer.GetText("SelectedSize")}: {FormatSize(viewModel.SelectedSize)}";
        dialog.IsPrimaryButtonEnabled = selectedCount > 0;

        if (selectAll is null)
        {
            return;
        }

        isUpdatingSelectAll = true;
        selectAll.IsChecked = selectedCount == 0
            ? false
            : selectedCount == viewModel.Files.Count
                ? true
                : null;
        isUpdatingSelectAll = false;
    }

    private void HandlePrimaryButtonClick(ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        if (session is null || viewModel is null || viewModel.GetSelectedFiles().Count == 0)
        {
            args.Cancel = true;
            return;
        }

        confirmed = true;
        _ = completion.TrySetResult(new TorrentConfirmationResult(session,
            viewModel.GetSelectedFiles(),
            downloadPath));
    }

    private void HandleWindowClosed(object sender,
        WindowEventArgs args)
    {
        closed = true;
        cancellation.Cancel();
        _ = completion.TrySetResult(null);
    }

    private async Task CloseAsync()
    {
        cancellation.Cancel();

        if (!confirmed && session is not null)
        {
            await coordinator.CancelAsync(session);
        }

        AnimateSmoke(0);

        if (!closed)
        {
            closed = true;
            dialog.PrimaryButtonClick -= HandlePrimaryButtonClick;
            dialog.Opened -= HandleDialogOpened;
            window.Closed -= HandleWindowClosed;
            window.Close();
        }

        cancellation.Dispose();
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

    private static Brush ResolveBrush(string resourceKey) => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out object value) && value is Brush brush
        ? brush
        : new SolidColorBrush(Windows.UI.Color.FromArgb(32, 255, 255, 255));

    private static Style? ResolveStyle(string resourceKey) => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out object value)
        ? value as Style
        : null;

    private void HandleDialogOpened(ContentDialog sender,
        ContentDialogOpenedEventArgs args) => SetActionButtonMinimumWidth(sender);

    private static void SetActionButtonMinimumWidth(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            if (child is Button button)
            {
                button.MinWidth = 132;
            }

            SetActionButtonMinimumWidth(child);
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.0} GB",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.0} MB",
        >= 1024 => $"{bytes / 1024d:0} KB",
        _ => $"{bytes} B"
    };
}
