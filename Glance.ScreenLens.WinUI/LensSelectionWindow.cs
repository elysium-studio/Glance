using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenLens.WinUI;

internal sealed class LensSelectionWindow
{
    private readonly LensBitmap bitmap;
    private readonly TaskCompletionSource<bool> presentationCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherQueue dispatcherQueue;
    private readonly Border highlight;
    private readonly Border instructionContainer;
    private readonly TextBlock instruction;
    private readonly ITextLocalizer localizer;
    private readonly Func<string, Task<bool>> copyAsync;
    private readonly Rectangle recognitionProgress;
    private readonly Storyboard recognitionProgressStoryboard;
    private readonly Func<LensRectangle, Task<LensRecognitionResult>> recognizeAsync;
    private readonly Grid root;
    private readonly Canvas selectionCanvas;
    private readonly GeometryGroup smokeGeometry;
    private readonly RectangleGeometry smokeBounds;
    private readonly Window window;
    private readonly nint windowHandle;
    private readonly HashSet<int> selectedWords = [];
    private readonly List<Border> wordHighlights = [];
    private Border? selectionMarquee;
    private Border? recognitionToolbar;
    private Button? copySelectionButton;
    private LensRecognitionResult recognition = LensRecognitionResult.Empty;
    private LensRegionAdjuster? regionAdjuster;
    private bool closed;
    private bool isDragging;
    private bool isSelectingWords;
    private int recognitionRequest;
    private LensRectangle selectedRegion;
    private Point selectionStart;
    private Point wordSelectionStart;

    private LensSelectionWindow(LensBitmap bitmap,
        ITextLocalizer localizer,
        Func<LensRectangle, Task<LensRecognitionResult>> recognizeAsync,
        Func<string, Task<bool>> copyAsync)
    {
        this.bitmap = bitmap;
        this.localizer = localizer;
        this.recognizeAsync = recognizeAsync;
        this.copyAsync = copyAsync;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        smokeBounds = new RectangleGeometry();
        smokeGeometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        smokeGeometry.Children.Add(smokeBounds);

        Microsoft.UI.Xaml.Shapes.Path smokeOverlay = new()
        {
            Data = smokeGeometry,
            Fill = ResolveBrush("SmokeFillColorDefaultBrush", Color.FromArgb(77, 0, 0, 0))
        };

        highlight = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 85, 214, 190)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Visibility = Visibility.Collapsed
        };

        selectionCanvas = new Canvas();
        selectionCanvas.Children.Add(highlight);
        recognitionProgress = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromArgb(255, 96, 205, 255)),
            StrokeThickness = 2,
            StrokeDashArray = [3, 2],
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        selectionCanvas.Children.Add(recognitionProgress);
        DoubleAnimation dashAnimation = new()
        {
            From = 0,
            To = -10,
            Duration = new Duration(TimeSpan.FromMilliseconds(650)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(dashAnimation, recognitionProgress);
        Storyboard.SetTargetProperty(dashAnimation, "StrokeDashOffset");
        recognitionProgressStoryboard = new Storyboard();
        recognitionProgressStoryboard.Children.Add(dashAnimation);
        instruction = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            Text = localizer.GetText("SelectionInstruction")
        };
        instructionContainer = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 24, 0, 0),
            Padding = new Thickness(14, 8, 14, 8),
            Background = ResolveBrush("AcrylicInAppFillColorDefaultBrush", Color.FromArgb(235, 32, 32, 32)),
            CornerRadius = new CornerRadius(8),
            Child = instruction
        };

        root = new Grid
        {
            Background = new ImageBrush { ImageSource = CreateImageSource(bitmap), Stretch = Stretch.Fill },
            IsTabStop = true
        };
        root.Children.Add(smokeOverlay);
        root.Children.Add(selectionCanvas);
        root.Children.Add(instructionContainer);
        root.KeyDown += HandleKeyDown;
        root.PointerMoved += HandleSelectionPointerMoved;
        root.PointerPressed += HandleSelectionPointerPressed;
        root.PointerReleased += HandleSelectionPointerReleased;
        root.Loaded += HandleLoaded;
        root.SizeChanged += HandleSizeChanged;

        window = new Window
        {
            Content = root,
            ExtendsContentIntoTitleBar = true,
            SystemBackdrop = new TransparentTintBackdrop()
        };
        window.SetTitleBar(null);
        window.Closed += HandleClosed;
        windowHandle = WindowNative.GetWindowHandle(window);
    }

    public static Task RunAsync(LensBitmap bitmap,
        ITextLocalizer localizer,
        Func<LensRectangle, Task<LensRecognitionResult>> recognizeAsync,
        Func<string, Task<bool>> copyAsync)
    {
        LensSelectionWindow selectionWindow = new(bitmap, localizer, recognizeAsync, copyAsync);
        selectionWindow.Show();
        return selectionWindow.presentationCompletion.Task;
    }

    private static WriteableBitmap CreateImageSource(LensBitmap bitmap)
    {
        WriteableBitmap imageSource = new(bitmap.Width, bitmap.Height);
        using Stream stream = imageSource.PixelBuffer.AsStream();
        stream.Write(bitmap.Pixels);
        imageSource.Invalidate();
        return imageSource;
    }

    private static Rect CreateRectangle(Point start, Point end) =>
        new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

    private static bool Intersects(Rect left, Rect right) =>
        left.X < right.Right && left.Right > right.X && left.Y < right.Bottom && left.Bottom > right.Y;

    private static bool IsControlInteraction(object source)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null)
        {
            if (current is Button or Thumb)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static Brush ResolveBrush(string key, Color fallback)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private void AttachWordSelectionHandlers()
    {
        root.PointerMoved += HandleWordPointerMoved;
        root.PointerPressed += HandleWordPointerPressed;
        root.PointerReleased += HandleWordPointerReleased;
    }

    private void BeginAdjustment(Rect initialBounds)
    {
        isDragging = false;
        root.ReleasePointerCaptures();
        DetachSelectionHandlers();
        highlight.Visibility = Visibility.Collapsed;
        regionAdjuster = new LensRegionAdjuster(root.ActualWidth, root.ActualHeight, initialBounds);
        regionAdjuster.BoundsChanged += HandleAdjustmentBoundsChanged;
        regionAdjuster.InteractionCompleted += HandleAdjustmentCompleted;
        regionAdjuster.InteractionStarted += HandleAdjustmentStarted;
        selectionCanvas.Children.Add(regionAdjuster);
        instructionContainer.Visibility = Visibility.Collapsed;
        UpdateAdjustmentMask();
        _ = RecognizeSelectionAsync();
    }

    private void BuildRecognitionMask()
    {
        smokeGeometry.Children.Clear();
        smokeGeometry.Children.Add(smokeBounds);

        foreach (LensRecognizedWord word in recognition.Words)
        {
            Rect bounds = ToLocal(new LensRectangle(selectedRegion.X + word.Bounds.X,
                selectedRegion.Y + word.Bounds.Y,
                word.Bounds.Width,
                word.Bounds.Height));
            smokeGeometry.Children.Add(new RectangleGeometry
            {
                Rect = new Rect(bounds.X - 2, bounds.Y - 1, bounds.Width + 4, bounds.Height + 2)
            });
        }
    }

    private void BuildToolbar()
    {
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        copySelectionButton = CreateTextButton(localizer.GetText("CopySelection"), CopySelection);
        copySelectionButton.IsEnabled = false;
        actions.Children.Add(copySelectionButton);

        Button copyAllButton = CreateTextButton(localizer.GetText("CopyAll"), CopyAll);
        copyAllButton.IsEnabled = recognition.Words.Count > 0;
        actions.Children.Add(copyAllButton);

        Button closeButton = new()
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 12,
            Content = "\uE711"
        };
        closeButton.Click += (_, _) => Close();
        ToolTipService.SetToolTip(closeButton, localizer.GetText("Close"));
        actions.Children.Add(closeButton);

        recognitionToolbar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 68, 0, 0),
            Padding = new Thickness(8),
            Background = ResolveBrush("AcrylicInAppFillColorDefaultBrush", Color.FromArgb(235, 32, 32, 32)),
            CornerRadius = new CornerRadius(10),
            Child = actions
        };
        root.Children.Add(recognitionToolbar);
    }

    private void BuildWordHighlights()
    {
        foreach (LensRecognizedWord word in recognition.Words)
        {
            Rect bounds = ToLocal(new LensRectangle(selectedRegion.X + word.Bounds.X,
                selectedRegion.Y + word.Bounds.Y,
                word.Bounds.Width,
                word.Bounds.Height));
            Border selection = new()
            {
                Width = Math.Max(1, bounds.Width + 4),
                Height = Math.Max(1, bounds.Height + 2),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 120, 212)),
                CornerRadius = new CornerRadius(2),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(selection, bounds.X - 2);
            Canvas.SetTop(selection, bounds.Y - 1);
            wordHighlights.Add(selection);
            selectionCanvas.Children.Add(selection);
        }

        selectionMarquee = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(28, 0, 120, 212)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(220, 96, 205, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        selectionCanvas.Children.Add(selectionMarquee);
    }

    private Button CreateTextButton(string text, RoutedEventHandler handler)
    {
        Button button = new()
        {
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            Content = text
        };
        button.Click += handler;
        return button;
    }

    private void Close()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        recognitionRequest++;
        recognitionProgressStoryboard.Stop();
        root.ReleasePointerCaptures();
        presentationCompletion.TrySetResult(true);
        window.Close();
    }

    private void ClearRecognitionSurface()
    {
        DetachWordSelectionHandlers();
        selectedWords.Clear();
        copySelectionButton = null;

        foreach (Border wordHighlight in wordHighlights)
        {
            selectionCanvas.Children.Remove(wordHighlight);
        }

        wordHighlights.Clear();

        if (selectionMarquee is not null)
        {
            selectionCanvas.Children.Remove(selectionMarquee);
            selectionMarquee = null;
        }

        if (recognitionToolbar is not null)
        {
            root.Children.Remove(recognitionToolbar);
            recognitionToolbar = null;
        }
    }

    private string ComposeSelection()
    {
        LensRecognizedWord[] words =
        [
            .. selectedWords
                .Select(index => recognition.Words[index])
                .OrderBy(word => word.LineIndex)
                .ThenBy(word => word.WordIndex)
        ];

        return string.Join(Environment.NewLine, words
            .GroupBy(word => word.LineIndex)
            .Select(line => string.Join(" ", line.Select(word => word.Text))));
    }

    private async void CopyAll(object sender, RoutedEventArgs args)
    {
        if (await copyAsync(recognition.Text))
        {
            Close();
        }
    }

    private async void CopySelection(object sender, RoutedEventArgs args)
    {
        string text = ComposeSelection();

        if (!string.IsNullOrWhiteSpace(text) && await copyAsync(text))
        {
            Close();
        }
    }

    private async Task RecognizeSelectionAsync()
    {
        if (closed || regionAdjuster is null)
        {
            return;
        }

        int request = ++recognitionRequest;
        Rect localBounds = regionAdjuster.Bounds;
        LensRectangle region = ToScreen(localBounds);
        selectedRegion = region;
        ClearRecognitionSurface();
        StartRecognitionProgress(localBounds);
        LensRecognitionResult result;

        try
        {
            result = await recognizeAsync(region);
        }
        catch
        {
            result = LensRecognitionResult.Empty;
        }

        await RunOnDispatcherAsync(() =>
        {
            if (closed || request != recognitionRequest)
            {
                return;
            }

            recognition = result;
            StopRecognitionProgress();
            BuildRecognitionMask();
            BuildWordHighlights();
            BuildToolbar();
            instruction.Text = result.Words.Count == 0
                ? localizer.GetText("NoTextInstruction")
                : localizer.GetText("SelectionReadyInstruction");
            instructionContainer.Visibility = Visibility.Visible;
            AttachWordSelectionHandlers();
        });
    }

    private Task RunOnDispatcherAsync(Action action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetCanceled();
        }

        return completion.Task;
    }

    private void StartRecognitionProgress(Rect bounds)
    {
        const double inset = 2;
        Canvas.SetLeft(recognitionProgress, bounds.X - inset);
        Canvas.SetTop(recognitionProgress, bounds.Y - inset);
        recognitionProgress.Width = bounds.Width + (inset * 2);
        recognitionProgress.Height = bounds.Height + (inset * 2);
        recognitionProgress.Visibility = Visibility.Visible;
        recognitionProgressStoryboard.Begin();
    }

    private void StopRecognitionProgress()
    {
        recognitionProgressStoryboard.Stop();
        recognitionProgress.Visibility = Visibility.Collapsed;
    }

    private void DetachSelectionHandlers()
    {
        root.PointerMoved -= HandleSelectionPointerMoved;
        root.PointerPressed -= HandleSelectionPointerPressed;
        root.PointerReleased -= HandleSelectionPointerReleased;
    }

    private void DetachWordSelectionHandlers()
    {
        root.PointerMoved -= HandleWordPointerMoved;
        root.PointerPressed -= HandleWordPointerPressed;
        root.PointerReleased -= HandleWordPointerReleased;
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        DetachSelectionHandlers();
        DetachWordSelectionHandlers();
        root.KeyDown -= HandleKeyDown;
        root.SizeChanged -= HandleSizeChanged;

        if (!closed)
        {
            closed = true;
            recognitionRequest++;
            recognitionProgressStoryboard.Stop();
            presentationCompletion.TrySetResult(true);
        }
    }

    private void HandleAdjustmentBoundsChanged(object? sender, EventArgs args) =>
        UpdateAdjustmentMask();

    private void HandleAdjustmentCompleted(object? sender, EventArgs args) =>
        _ = RecognizeSelectionAsync();

    private void HandleAdjustmentStarted(object? sender, EventArgs args)
    {
        recognitionRequest++;
        StopRecognitionProgress();
        ClearRecognitionSurface();
        instructionContainer.Visibility = Visibility.Collapsed;
        UpdateAdjustmentMask();
    }

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            Close();
        }
    }

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        root.Loaded -= HandleLoaded;
        root.UpdateLayout();
        UpdateSmokeBounds();
        window.Activate();
        root.Focus(FocusState.Programmatic);
    }

    private void HandleSelectionPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (isDragging)
        {
            ShowSelectionHighlight(CreateRectangle(selectionStart, args.GetCurrentPoint(root).Position));
        }
    }

    private void HandleSelectionPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Microsoft.UI.Input.PointerPoint point = args.GetCurrentPoint(root);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        selectionStart = point.Position;
        isDragging = true;
        root.CapturePointer(args.Pointer);
        ShowSelectionHighlight(CreateRectangle(selectionStart, selectionStart));
        args.Handled = true;
    }

    private void HandleSelectionPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        root.ReleasePointerCapture(args.Pointer);
        Rect local = CreateRectangle(selectionStart, args.GetCurrentPoint(root).Position);
        args.Handled = true;

        if (local.Width < 4 || local.Height < 4)
        {
            highlight.Visibility = Visibility.Collapsed;
            return;
        }

        BeginAdjustment(local);
    }

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateSmokeBounds();

    private void HandleWordPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!isSelectingWords || selectionMarquee is null)
        {
            return;
        }

        ShowWordSelectionMarquee(CreateRectangle(wordSelectionStart, args.GetCurrentPoint(root).Position));
    }

    private void HandleWordPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (IsControlInteraction(args.OriginalSource))
        {
            return;
        }

        Microsoft.UI.Input.PointerPoint point = args.GetCurrentPoint(root);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        wordSelectionStart = point.Position;
        isSelectingWords = true;
        root.CapturePointer(args.Pointer);
        ShowWordSelectionMarquee(CreateRectangle(wordSelectionStart, wordSelectionStart));
        args.Handled = true;
    }

    private void HandleWordPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!isSelectingWords || selectionMarquee is null)
        {
            return;
        }

        isSelectingWords = false;
        root.ReleasePointerCapture(args.Pointer);
        Point end = args.GetCurrentPoint(root).Position;
        Rect selection = CreateRectangle(wordSelectionStart, end);
        selectionMarquee.Visibility = Visibility.Collapsed;
        selectedWords.Clear();

        for (int index = 0; index < wordHighlights.Count; index++)
        {
            Border wordHighlight = wordHighlights[index];
            Rect bounds = new(Canvas.GetLeft(wordHighlight), Canvas.GetTop(wordHighlight), wordHighlight.Width, wordHighlight.Height);
            bool selected = selection.Width < 4 && selection.Height < 4
                ? bounds.Contains(end)
                : Intersects(bounds, selection);

            wordHighlight.Background = new SolidColorBrush(selected
                ? Color.FromArgb(96, 0, 120, 212)
                : Color.FromArgb(0, 0, 120, 212));

            if (selected)
            {
                selectedWords.Add(index);
            }
        }

        if (copySelectionButton is not null)
        {
            copySelectionButton.IsEnabled = selectedWords.Count > 0;
        }

        args.Handled = true;
    }

    private void Show()
    {
        OverlappedPresenter presenter = window.AppWindow.Presenter.As<OverlappedPresenter>();
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);
        window.AppWindow.IsShownInSwitchers = false;
        window.AppWindow.MoveAndResize(new RectInt32(bitmap.OriginX, bitmap.OriginY, bitmap.Width, bitmap.Height));
        window.AppWindow.Show(false);
    }

    private void ShowSelectionHighlight(Rect rectangle)
    {
        smokeGeometry.Children.Clear();
        smokeGeometry.Children.Add(smokeBounds);
        smokeGeometry.Children.Add(new RectangleGeometry { Rect = rectangle });
        Canvas.SetLeft(highlight, rectangle.X);
        Canvas.SetTop(highlight, rectangle.Y);
        highlight.Width = rectangle.Width;
        highlight.Height = rectangle.Height;
        highlight.Visibility = Visibility.Visible;
    }

    private void ShowWordSelectionMarquee(Rect rectangle)
    {
        if (selectionMarquee is null)
        {
            return;
        }

        Canvas.SetLeft(selectionMarquee, rectangle.X);
        Canvas.SetTop(selectionMarquee, rectangle.Y);
        selectionMarquee.Width = rectangle.Width;
        selectionMarquee.Height = rectangle.Height;
        selectionMarquee.Visibility = Visibility.Visible;
    }

    private Rect ToLocal(LensRectangle rectangle)
    {
        double scaleX = root.ActualWidth / bitmap.Width;
        double scaleY = root.ActualHeight / bitmap.Height;
        return new Rect((rectangle.X - bitmap.OriginX) * scaleX,
            (rectangle.Y - bitmap.OriginY) * scaleY,
            rectangle.Width * scaleX,
            rectangle.Height * scaleY);
    }

    private LensRectangle ToScreen(Rect rectangle)
    {
        double scaleX = bitmap.Width / root.ActualWidth;
        double scaleY = bitmap.Height / root.ActualHeight;
        return new LensRectangle(bitmap.OriginX + (int)Math.Round(rectangle.X * scaleX),
            bitmap.OriginY + (int)Math.Round(rectangle.Y * scaleY),
            (int)Math.Round(rectangle.Width * scaleX),
            (int)Math.Round(rectangle.Height * scaleY));
    }

    private void UpdateSmokeBounds() =>
        smokeBounds.Rect = new Rect(0, 0, root.ActualWidth, root.ActualHeight);

    private void UpdateAdjustmentMask()
    {
        if (regionAdjuster is null)
        {
            return;
        }

        smokeGeometry.Children.Clear();
        smokeGeometry.Children.Add(smokeBounds);
        smokeGeometry.Children.Add(new RectangleGeometry
        {
            Rect = regionAdjuster.Bounds
        });
    }
}
