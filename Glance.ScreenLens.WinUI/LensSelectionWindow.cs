using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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
    private readonly List<LensSelectableWord> selectableWords = [];
    private readonly List<LensSelectionRow> selectionRows = [];
    private readonly List<Border> wordHighlights = [];
    private LensTextSelectionSurface? textSelectionSurface;
    private Border? recognitionToolbar;
    private Button? copySelectionButton;
    private LensRecognitionResult recognition = LensRecognitionResult.Empty;
    private ResizableRegionOverlay? regionAdjuster;
    private bool closed;
    private bool isDragging;
    private bool isSelectingText;
    private int recognitionRequest;
    private int selectionAnchor = -1;
    private int selectionFocus = -1;
    private LensRectangle selectedRegion;
    private Point selectionStart;

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
        Canvas.SetZIndex(recognitionProgress, 5);
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
            Margin = new Thickness(0, 40, 0, 0),
            Padding = new Thickness(18, 10, 18, 10),
            Background = OverlayChrome.CreateAcrylicBrush(),
            BorderBrush = ResolveBrush("SurfaceStrokeColorDefaultBrush", Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = instruction
        };
        OverlayChrome.Elevate(instructionContainer);

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
        KeyboardAccelerator copyAccelerator = new()
        {
            Key = Windows.System.VirtualKey.C,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        copyAccelerator.Invoked += HandleCopyAccelerator;
        root.KeyboardAccelerators.Add(copyAccelerator);

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

    private static bool SharesVisualRow(IReadOnlyList<LensWordCandidate> row, LensWordCandidate candidate)
    {
        double rowTop = row.Min(word => word.Bounds.Top);
        double rowBottom = row.Max(word => word.Bounds.Bottom);
        double overlap = Math.Min(rowBottom, candidate.Bounds.Bottom) - Math.Max(rowTop, candidate.Bounds.Top);
        double minimumHeight = Math.Min(rowBottom - rowTop, candidate.Bounds.Height);
        double rowCenter = (rowTop + rowBottom) / 2;
        double candidateCenter = candidate.Bounds.Top + (candidate.Bounds.Height / 2);
        return overlap >= minimumHeight * 0.35 || Math.Abs(rowCenter - candidateCenter) <= Math.Max(3, minimumHeight * 0.45);
    }

    private static PathGeometry CreateRoundedRectangleGeometry(Rect bounds)
    {
        double radius = Math.Min(bounds.Width / 2, bounds.Height / 2);
        PathFigure figure = new()
        {
            StartPoint = new Point(bounds.X + radius, bounds.Y),
            IsClosed = true,
            Segments =
            [
                new LineSegment { Point = new Point(bounds.Right - radius, bounds.Y) },
                new ArcSegment { Point = new Point(bounds.Right, bounds.Y + radius), Size = new Size(radius, radius), SweepDirection = SweepDirection.Clockwise },
                new LineSegment { Point = new Point(bounds.Right, bounds.Bottom - radius) },
                new ArcSegment { Point = new Point(bounds.Right - radius, bounds.Bottom), Size = new Size(radius, radius), SweepDirection = SweepDirection.Clockwise },
                new LineSegment { Point = new Point(bounds.X + radius, bounds.Bottom) },
                new ArcSegment { Point = new Point(bounds.X, bounds.Bottom - radius), Size = new Size(radius, radius), SweepDirection = SweepDirection.Clockwise },
                new LineSegment { Point = new Point(bounds.X, bounds.Y + radius) },
                new ArcSegment { Point = new Point(bounds.X + radius, bounds.Y), Size = new Size(radius, radius), SweepDirection = SweepDirection.Clockwise }
            ]
        };
        return new PathGeometry { Figures = [figure] };
    }

    private static Brush ResolveBrush(string key, Color fallback)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private void BeginAdjustment(Rect initialBounds)
    {
        isDragging = false;
        root.ReleasePointerCaptures();
        DetachSelectionHandlers();
        highlight.Visibility = Visibility.Collapsed;
        regionAdjuster = new ResizableRegionOverlay(root.ActualWidth, root.ActualHeight, bitmap.Width, bitmap.Height, initialBounds, false, false);
        regionAdjuster.BoundsChanged += HandleAdjustmentBoundsChanged;
        regionAdjuster.InteractionCompleted += HandleAdjustmentCompleted;
        regionAdjuster.InteractionStarted += HandleAdjustmentStarted;
        Canvas.SetLeft(regionAdjuster, -ResizableRegionOverlay.VisualPadding);
        Canvas.SetTop(regionAdjuster, -ResizableRegionOverlay.VisualPadding);
        Canvas.SetZIndex(regionAdjuster, 3);
        selectionCanvas.Children.Add(regionAdjuster);
        instructionContainer.Visibility = Visibility.Collapsed;
        UpdateAdjustmentMask();
        _ = RecognizeSelectionAsync();
    }

    private void BuildRecognitionMask()
    {
        smokeGeometry.Children.Clear();
        smokeGeometry.Children.Add(smokeBounds);

        foreach (LensRecognizedLine line in recognition.Lines)
        {
            Rect bounds = ToLocal(line.Bounds);
            smokeGeometry.Children.Add(CreateRoundedRectangleGeometry(new Rect(bounds.X - 5, bounds.Y - 2, bounds.Width + 10, bounds.Height + 4)));
        }
    }

    private void BuildToolbar()
    {
        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        copySelectionButton = CreateIconButton("\uF683", localizer.GetText("CopySelection"), CopySelection);
        copySelectionButton.IsEnabled = false;
        actions.Children.Add(copySelectionButton);

        Button copyAllButton = CreateIconButton("\uE8C8", localizer.GetText("CopyAll"), CopyAll);
        copyAllButton.IsEnabled = recognition.Lines.Count > 0;
        actions.Children.Add(copyAllButton);

        Button closeButton = CreateIconButton("\uE711", localizer.GetText("Close"), (_, _) => Close());
        actions.Children.Add(closeButton);

        recognitionToolbar = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(8),
            Background = OverlayChrome.CreateAcrylicBrush(),
            BorderBrush = ResolveBrush("SurfaceStrokeColorDefaultBrush", Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = actions
        };
        OverlayChrome.Elevate(recognitionToolbar, 40);
        root.Children.Add(recognitionToolbar);
        PositionToolbar();
    }

    private void BuildTextSelectionLayer()
    {
        Rect regionBounds = ToLocal(selectedRegion);
        List<LensWordCandidate> candidates =
        [
            .. recognition.Words.Select(word =>
            {
                Rect bounds = ToLocal(word.Bounds);
                return new LensWordCandidate(word,
                    new Rect(bounds.X - regionBounds.X, bounds.Y - regionBounds.Y, bounds.Width, bounds.Height));
            }).OrderBy(candidate => candidate.Bounds.Top).ThenBy(candidate => candidate.Bounds.Left)
        ];
        List<List<LensWordCandidate>> visualRows = [];

        foreach (LensWordCandidate candidate in candidates)
        {
            List<LensWordCandidate>? row = visualRows
                .Where(existing => SharesVisualRow(existing, candidate))
                .MinBy(existing => Math.Abs(existing.Average(word => word.Bounds.Top + (word.Bounds.Height / 2)) -
                    (candidate.Bounds.Top + (candidate.Bounds.Height / 2))));

            if (row is null)
            {
                row = [];
                visualRows.Add(row);
            }

            row.Add(candidate);
        }

        visualRows = [.. visualRows
            .OrderBy(row => row.Min(word => word.Bounds.Top))
            .ThenBy(row => row.Min(word => word.Bounds.Left))];
        textSelectionSurface = new LensTextSelectionSurface
        {
            Width = regionBounds.Width,
            Height = regionBounds.Height
        };
        textSelectionSurface.PointerMoved += HandleTextPointerMoved;
        textSelectionSurface.PointerPressed += HandleTextPointerPressed;
        textSelectionSurface.PointerReleased += HandleTextPointerReleased;
        Canvas.SetLeft(textSelectionSurface, regionBounds.X);
        Canvas.SetTop(textSelectionSurface, regionBounds.Y);
        Canvas.SetZIndex(textSelectionSurface, 2);

        for (int rowIndex = 0; rowIndex < visualRows.Count; rowIndex++)
        {
            List<LensWordCandidate> row = [.. visualRows[rowIndex].OrderBy(candidate => candidate.Bounds.Left)];
            int rowStartIndex = selectableWords.Count;

            foreach (LensWordCandidate candidate in row)
            {
                Rect bounds = candidate.Bounds;
                Border highlight = new()
                {
                    Width = Math.Max(1, bounds.Width + 4),
                    Height = Math.Max(1, bounds.Height + 2),
                    Background = new SolidColorBrush(Color.FromArgb(0, 0, 120, 212)),
                    CornerRadius = new CornerRadius(Math.Max(1, (bounds.Height + 2) / 2)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(highlight, bounds.X - 2);
                Canvas.SetTop(highlight, bounds.Y - 1);
                selectableWords.Add(new LensSelectableWord(candidate.Word, bounds, rowIndex));
                wordHighlights.Add(highlight);
                textSelectionSurface.Children.Add(highlight);
            }

            selectionRows.Add(new LensSelectionRow(rowStartIndex,
                row.Count,
                row.Min(candidate => candidate.Bounds.Top),
                row.Max(candidate => candidate.Bounds.Bottom)));
        }

        selectionCanvas.Children.Add(textSelectionSurface);
    }

    private Button CreateIconButton(string glyph, string label, RoutedEventHandler handler)
    {
        Button button = new()
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Content = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 14,
            Style = Microsoft.UI.Xaml.Application.Current.Resources["SubtleButtonStyle"] as Style
        };
        button.Click += handler;
        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
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
        selectedWords.Clear();
        selectionAnchor = -1;
        selectionFocus = -1;
        copySelectionButton = null;
        selectableWords.Clear();
        selectionRows.Clear();
        wordHighlights.Clear();

        if (textSelectionSurface is not null)
        {
            textSelectionSurface.PointerMoved -= HandleTextPointerMoved;
            textSelectionSurface.PointerPressed -= HandleTextPointerPressed;
            textSelectionSurface.PointerReleased -= HandleTextPointerReleased;
            selectionCanvas.Children.Remove(textSelectionSurface);
            textSelectionSurface = null;
        }

        if (recognitionToolbar is not null)
        {
            root.Children.Remove(recognitionToolbar);
            recognitionToolbar = null;
        }
    }

    private string ComposeSelection()
    {
        LensSelectableWord[] words =
        [
            .. selectedWords
                .Select(index => selectableWords[index])
                .OrderBy(word => word.RowIndex)
                .ThenBy(word => word.Bounds.Left)
        ];
        return string.Join(Environment.NewLine, words
            .GroupBy(word => word.RowIndex)
            .Select(row => string.Join(" ", row.Select(word => word.Word.Text))));
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
        Rect localBounds = regionAdjuster.CropBounds;
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
            BuildTextSelectionLayer();
            BuildToolbar();
            instruction.Text = result.Lines.Count == 0
                ? localizer.GetText("NoTextInstruction")
                : localizer.GetText("SelectionReadyInstruction");
            instructionContainer.Visibility = Visibility.Visible;
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

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        DetachSelectionHandlers();
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

    private async void HandleCopyAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        string text = ComposeSelection();

        if (!string.IsNullOrWhiteSpace(text))
        {
            args.Handled = true;
            await copyAsync(text);
        }
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

    private void HandleSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateSmokeBounds();
        PositionToolbar();
    }

    private void HandleTextPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!isSelectingText || textSelectionSurface is null)
        {
            return;
        }

        int index = FindWordIndex(args.GetCurrentPoint(textSelectionSurface).Position, true);

        if (index >= 0 && index != selectionFocus)
        {
            selectionFocus = index;
            UpdateTextSelection();
        }

        args.Handled = true;
    }

    private void HandleTextPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (textSelectionSurface is null)
        {
            return;
        }

        Microsoft.UI.Input.PointerPoint point = args.GetCurrentPoint(textSelectionSurface);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        int index = FindWordIndex(point.Position, false);

        if (index < 0)
        {
            ClearTextSelection();
            return;
        }

        selectionAnchor = index;
        selectionFocus = index;
        isSelectingText = true;
        textSelectionSurface.CapturePointer(args.Pointer);
        UpdateTextSelection();
        args.Handled = true;
    }

    private void HandleTextPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (!isSelectingText || textSelectionSurface is null)
        {
            return;
        }

        isSelectingText = false;
        textSelectionSurface.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private int FindWordIndex(Point point, bool useNearest)
    {
        for (int index = 0; index < wordHighlights.Count; index++)
        {
            Border highlight = wordHighlights[index];
            Rect bounds = new(Canvas.GetLeft(highlight), Canvas.GetTop(highlight), highlight.Width, highlight.Height);

            if (bounds.Contains(point))
            {
                return index;
            }
        }

        if (!useNearest || selectionRows.Count == 0)
        {
            return -1;
        }

        LensSelectionRow row = selectionRows.MinBy(candidate => point.Y < candidate.Top
            ? candidate.Top - point.Y
            : point.Y > candidate.Bottom
                ? point.Y - candidate.Bottom
                : 0)!;
        int firstIndex = row.StartIndex;
        int lastIndex = row.StartIndex + row.Count - 1;

        if (point.X <= selectableWords[firstIndex].Bounds.Left)
        {
            return firstIndex;
        }

        if (point.X >= selectableWords[lastIndex].Bounds.Right)
        {
            return lastIndex;
        }

        return Enumerable.Range(firstIndex, row.Count).MinBy(index =>
        {
            Rect bounds = selectableWords[index].Bounds;
            return point.X < bounds.Left
                ? bounds.Left - point.X
                : point.X > bounds.Right
                    ? point.X - bounds.Right
                    : 0;
        });
    }

    private void ClearTextSelection()
    {
        selectedWords.Clear();
        selectionAnchor = -1;
        selectionFocus = -1;

        foreach (Border highlight in wordHighlights)
        {
            highlight.Background = new SolidColorBrush(Color.FromArgb(0, 0, 120, 212));
        }

        if (copySelectionButton is not null)
        {
            copySelectionButton.IsEnabled = false;
        }
    }

    private void UpdateTextSelection()
    {
        int first = Math.Min(selectionAnchor, selectionFocus);
        int last = Math.Max(selectionAnchor, selectionFocus);
        selectedWords.Clear();

        for (int index = 0; index < wordHighlights.Count; index++)
        {
            bool selected = index >= first && index <= last;
            wordHighlights[index].Background = new SolidColorBrush(selected
                ? Color.FromArgb(112, 0, 120, 212)
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

    private void PositionToolbar()
    {
        if (recognitionToolbar is null || root.ActualWidth <= 0 || root.ActualHeight <= 0)
        {
            return;
        }

        const double edgePadding = 12;
        const double regionGap = 12;
        recognitionToolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size toolbarSize = recognitionToolbar.DesiredSize;
        Rect regionBounds = ToLocal(selectedRegion);
        double x = Math.Clamp(regionBounds.X + ((regionBounds.Width - toolbarSize.Width) / 2),
            edgePadding,
            Math.Max(edgePadding, root.ActualWidth - toolbarSize.Width - edgePadding));
        double above = regionBounds.Y - toolbarSize.Height - regionGap;
        double below = regionBounds.Bottom + regionGap;
        double y;

        if (above >= edgePadding)
        {
            y = above;
        }
        else if (below + toolbarSize.Height <= root.ActualHeight - edgePadding)
        {
            y = below;
        }
        else
        {
            y = Math.Clamp(regionBounds.Y + regionGap,
                edgePadding,
                Math.Max(edgePadding, root.ActualHeight - toolbarSize.Height - edgePadding));
        }

        recognitionToolbar.Margin = new Thickness(x, y, 0, 0);
    }

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
            Rect = regionAdjuster.CropBounds
        });
    }

    private sealed record LensSelectableWord(LensRecognizedWord Word,
        Rect Bounds,
        int RowIndex);

    private sealed record LensSelectionRow(int StartIndex,
        int Count,
        double Top,
        double Bottom);

    private sealed record LensWordCandidate(LensRecognizedWord Word,
        Rect Bounds);
}
