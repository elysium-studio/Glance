using Elysium.Platform.Windows;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using PlatformWindowExtensions = Elysium.Platform.Windows.WindowExtensions;

namespace Glance.ScreenRecorder.WinUI;

internal sealed class RecordingReviewWindow
{
    private const int CaptureBeatDurationMs = 83;
    private const int CaptureHoldDurationMs = 50;
    private const int DismissDurationMs = 240;
    private const int EntranceDurationMs = 360;
    private const int ExtendedWindowStyleIndex = -20;
    private const int FlightDurationMs = 250;
    private const int FlightAnimationDurationMs = CaptureBeatDurationMs + CaptureHoldDurationMs + FlightDurationMs;
    private const int NoActivateExtendedWindowStyle = 0x08000000;
    private const int TransparentExtendedWindowStyle = 0x00000020;
    private readonly NativeRectangle desktopBounds;
    private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MediaPlayer mediaPlayer;
    private readonly MediaPlayerElement playerElement;
    private readonly ImageSource previewImage;
    private readonly Border previewHost;
    private readonly Rect previewBounds;
    private readonly Border reviewBackdrop;
    private readonly Canvas reviewCanvas;
    private readonly Grid root;
    private readonly NativeRectangle sourceBounds;
    private readonly Border toolbar;
    private readonly Window window;
    private readonly nint windowHandle;
    private DispatcherQueueTimer? dismissTimer;
    private DispatcherQueueTimer? entranceTimer;
    private EventHandler<object>? entranceRenderingHandler;
    private bool closed;
    private bool completed;
    private bool flightInProgress;
    private bool transitioning;
    private Border? interactivePreviewHost;

    private RecordingReviewWindow(StorageFile file,
        ImageSource previewImage,
        NativeRectangle sourceBounds,
        NativeRectangle desktopBounds,
        ITextLocalizer localizer,
        Window window,
        Grid root,
        nint windowHandle)
    {
        this.sourceBounds = sourceBounds;
        this.desktopBounds = desktopBounds;
        this.window = window;
        this.root = root;
        this.windowHandle = windowHandle;
        this.previewImage = previewImage;
        double availableWidth = desktopBounds.Width;
        double availableHeight = desktopBounds.Height;
        double maximumWidth = Math.Max(320, availableWidth - 96);
        double maximumHeight = Math.Max(180, availableHeight - 180);
        double scale = Math.Min(1, Math.Min(maximumWidth / sourceBounds.Width, maximumHeight / sourceBounds.Height));
        double previewWidth = Math.Max(1, Math.Round(sourceBounds.Width * scale));
        double previewHeight = Math.Max(1, Math.Round(sourceBounds.Height * scale));
        double previewX = Math.Round((availableWidth - previewWidth) / 2);
        double previewY = Math.Round((availableHeight - previewHeight + 42) / 2);
        previewBounds = new Rect(previewX, previewY, previewWidth, previewHeight);

        mediaPlayer = new MediaPlayer
        {
            AutoPlay = false,
            IsLoopingEnabled = false,
            Source = MediaSource.CreateFromStorageFile(file)
        };
        playerElement = new MediaPlayerElement
        {
            AreTransportControlsEnabled = true,
            IsEnabled = true,
            IsHitTestVisible = true,
            PosterSource = previewImage,
            Stretch = Stretch.Uniform
        };
        playerElement.SetMediaPlayer(mediaPlayer);
        Image animationPreview = new()
        {
            Source = previewImage,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        previewHost = CreatePreviewHost(animationPreview, previewWidth, previewHeight);
        previewHost.IsHitTestVisible = false;
        ElementCompositionPreview.SetIsTranslationEnabled(previewHost, true);
        previewHost.Translation = new Vector3(0, 0, 32);
        Canvas.SetLeft(previewHost, previewX);
        Canvas.SetTop(previewHost, previewY);

        Button dismissButton = CreateToolbarButton("\uE711", localizer.GetText("DismissRecordingReview"), false);
        dismissButton.Click += (_, _) => Dismiss();
        Button confirmButton = CreateToolbarButton("\uE73E", localizer.GetText("ConfirmRecordingReview"), true);
        confirmButton.Click += (_, _) => Confirm();
        StackPanel toolbarContent = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        toolbarContent.Children.Add(dismissButton);
        toolbarContent.Children.Add(confirmButton);
        toolbar = new Border
        {
            Padding = new Thickness(6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = OverlayChrome.CreateAcrylicBrush(),
            BorderBrush = ResolveBrush("SurfaceStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(48, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Child = toolbarContent
        };
        OverlayChrome.Elevate(toolbar, 48);
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        toolbar.Margin = new Thickness(0, Math.Max(20, previewY - toolbar.DesiredSize.Height - 14), 0, 0);

        reviewBackdrop = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(178, 8, 10, 14)),
            IsHitTestVisible = false
        };
        reviewCanvas = new Canvas { IsHitTestVisible = true };
        reviewCanvas.Children.Add(previewHost);
        root.Children.Clear();
        root.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0));
        root.IsHitTestVisible = true;
        root.IsTabStop = true;
        root.Children.Add(reviewBackdrop);
        root.Children.Add(reviewCanvas);
        root.Children.Add(toolbar);
        root.KeyDown += HandleKeyDown;
        window.Closed += HandleClosed;
    }

    public static async Task<RecordingReviewWindow?> ReviewAsync(string filePath,
        NativeRectangle sourceBounds,
        NativeRectangle desktopBounds,
        ITextLocalizer localizer,
        Window window,
        Grid root,
        nint windowHandle)
    {
        StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        TaskCompletionSource<RecordingReviewWindow?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        async void ShowReview()
        {
            try
            {
                IRandomAccessStreamWithContentType thumbnail = await CreatePreviewThumbnailAsync(file, sourceBounds);

                if (window.DispatcherQueue.HasThreadAccess)
                {
                    PresentReview(thumbnail);
                }
                else if (!window.DispatcherQueue.TryEnqueue(() => PresentReview(thumbnail)))
                {
                    thumbnail.Dispose();
                    _ = result.TrySetException(new InvalidOperationException("Unable to present the recording review."));
                }
            }
            catch (Exception exception)
            {
                _ = result.TrySetException(exception);
            }
        }

        async void PresentReview(IRandomAccessStreamWithContentType thumbnail)
        {
            RecordingReviewWindow? review = null;

            try
            {
                BitmapImage previewImage = new();
                thumbnail.Seek(0);
                previewImage.SetSource(thumbnail);
                thumbnail.Dispose();
                review = new RecordingReviewWindow(file,
                    previewImage,
                    sourceBounds,
                    desktopBounds,
                    localizer,
                    window,
                    root,
                    windowHandle);
                review.Show();
                bool confirmed = await review.completion.Task;
                _ = result.TrySetResult(confirmed ? review : null);
            }
            catch (Exception exception)
            {
                thumbnail.Dispose();
                review?.Close();
                _ = result.TrySetException(exception);
            }
        }

        if (window.DispatcherQueue.HasThreadAccess)
        {
            ShowReview();
        }
        else if (!window.DispatcherQueue.TryEnqueue(ShowReview))
        {
            _ = result.TrySetException(new InvalidOperationException("Unable to open the recording review."));
        }

        return await result.Task;
    }

    private static async Task<IRandomAccessStreamWithContentType> CreatePreviewThumbnailAsync(StorageFile file, NativeRectangle bounds)
    {
        double scale = Math.Min(1, Math.Min(1920d / Math.Max(1, bounds.Width), 1080d / Math.Max(1, bounds.Height)));
        int width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
        MediaClip clip = await MediaClip.CreateFromFileAsync(file);
        MediaComposition composition = new();
        composition.Clips.Add(clip);
        return await composition.GetThumbnailAsync(TimeSpan.Zero,
            width,
            height,
            VideoFramePrecision.NearestFrame);
    }

    public void Close()
    {
        if (window.DispatcherQueue.HasThreadAccess)
        {
            CloseCore();
        }
        else
        {
            _ = window.DispatcherQueue.TryEnqueue(CloseCore);
        }
    }

    public Task PlayFlightAsync(NativeRectangle landingBounds, Action onArrived)
    {
        TaskCompletionSource flightCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void Play()
        {
            try
            {
                PlayFlight(landingBounds, onArrived, flightCompletion);
            }
            catch (Exception exception)
            {
                CloseCore();
                _ = flightCompletion.TrySetException(exception);
            }
        }

        if (window.DispatcherQueue.HasThreadAccess)
        {
            Play();
        }
        else if (!window.DispatcherQueue.TryEnqueue(Play))
        {
            _ = flightCompletion.TrySetException(new InvalidOperationException("Unable to start the recording handoff animation."));
        }

        return flightCompletion.Task;
    }

    private void Show()
    {
        AppWindow appWindow = window.AppWindow;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        PlatformWindowExtensions.SetBorderless(windowHandle, true);
        PlatformWindowExtensions.SetCornerRadius(windowHandle, WindowCornerPreference.Sharp);
        PlatformWindowExtensions.SetTopMost(windowHandle, true);
        PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
        appWindow.IsShownInSwitchers = false;
        appWindow.MoveAndResize(new RectInt32(desktopBounds.Left, desktopBounds.Top, desktopBounds.Width, desktopBounds.Height));
        ActivateReviewInput();
        root.UpdateLayout();
        PlayEntrance();
        _ = DwmFlush();
        PlatformWindowExtensions.viSetOpacity(windowHandle, 255);
    }

    private void ActivateReviewInput()
    {
        int extendedStyle = GetWindowLong(windowHandle, ExtendedWindowStyleIndex);
        extendedStyle &= ~(NoActivateExtendedWindowStyle | TransparentExtendedWindowStyle);
        _ = SetWindowLong(windowHandle, ExtendedWindowStyleIndex, extendedStyle);
        _ = EnableWindow(windowHandle, true);
        window.Activate();
        _ = SetForegroundWindow(windowHandle);
        root.IsHitTestVisible = true;
        _ = root.Focus(FocusState.Programmatic);
    }

    private void PlayEntrance()
    {
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Rect source = ToLocal(sourceBounds);
        Vector3 targetTranslation = previewHost.Translation;
        Vector3 sourceCenter = new((float)(source.X + (source.Width / 2)), (float)(source.Y + (source.Height / 2)), 0);
        Vector3 targetCenter = new((float)(previewBounds.X + (previewBounds.Width / 2)), (float)(previewBounds.Y + (previewBounds.Height / 2)), 0);
        Vector3 sourceTranslation = targetTranslation + sourceCenter - targetCenter;
        Vector3 sourceScale = new((float)Math.Max(0.01, source.Width / previewBounds.Width),
            (float)Math.Max(0.01, source.Height / previewBounds.Height),
            1);

        previewVisual.CenterPoint = new Vector3((float)previewBounds.Width / 2, (float)previewBounds.Height / 2, 0);
        previewHost.Translation = sourceTranslation;
        previewVisual.Scale = sourceScale;
        previewVisual.Opacity = 1;
        backdropVisual.Opacity = 0;
        int frames = 0;
        entranceRenderingHandler = (_, _) =>
        {
            if (++frames < 2)
            {
                return;
            }

            CompositionTarget.Rendering -= entranceRenderingHandler;
            entranceRenderingHandler = null;
            StartEntranceAnimations(previewVisual,
                backdropVisual,
                targetTranslation,
                sourceTranslation,
                sourceScale);
        };
        CompositionTarget.Rendering += entranceRenderingHandler;
    }

    private void StartEntranceAnimations(Visual previewVisual,
        Visual backdropVisual,
        Vector3 targetTranslation,
        Vector3 sourceTranslation,
        Vector3 sourceScale)
    {
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(EntranceDurationMs);
        CubicBezierEasingFunction travelEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 0.84f), new Vector2(0.28f, 1));
        SineEasingFunction fadeEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);
        previewHost.Translation = targetTranslation;
        previewVisual.Scale = Vector3.One;
        previewVisual.StartAnimation("Translation", CreateVectorAnimation(compositor, sourceTranslation, targetTranslation, duration, travelEasing));
        previewVisual.StartAnimation(nameof(Visual.Scale), CreateVectorAnimation(compositor, sourceScale, Vector3.One, duration, travelEasing));
        backdropVisual.Opacity = 1;
        backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 0, 1, duration, fadeEasing));
        entranceTimer = root.DispatcherQueue.CreateTimer();
        entranceTimer.Interval = duration;
        entranceTimer.IsRepeating = false;
        entranceTimer.Tick += HandleEntranceCompleted;
        entranceTimer.Start();
    }

    private void HandleEntranceCompleted(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= HandleEntranceCompleted;
        entranceTimer = null;
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        previewVisual.StopAnimation("Translation");
        previewVisual.StopAnimation(nameof(Visual.Scale));
        previewVisual.Scale = Vector3.One;
        AttachInteractivePreview();
    }

    private void AttachInteractivePreview()
    {
        if (interactivePreviewHost is not null)
        {
            return;
        }

        playerElement.AreTransportControlsEnabled = true;
        playerElement.IsEnabled = true;
        playerElement.IsHitTestVisible = true;
        interactivePreviewHost = CreatePreviewHost(playerElement, previewBounds.Width, previewBounds.Height);
        interactivePreviewHost.HorizontalAlignment = HorizontalAlignment.Left;
        interactivePreviewHost.VerticalAlignment = VerticalAlignment.Top;
        interactivePreviewHost.Margin = new Thickness(previewBounds.X, previewBounds.Y, 0, 0);
        interactivePreviewHost.IsHitTestVisible = true;
        int toolbarIndex = root.Children.IndexOf(toolbar);
        root.Children.Insert(Math.Max(0, toolbarIndex), interactivePreviewHost);
        _ = reviewCanvas.Children.Remove(previewHost);
        root.UpdateLayout();
    }

    private void HandleKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Escape)
        {
            args.Handled = true;
            Dismiss();
        }
        else if (args.Key == Windows.System.VirtualKey.Enter && args.OriginalSource is not Button and not Slider)
        {
            args.Handled = true;
            Confirm();
        }
    }

    private void Confirm()
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        StopEntrance();
        DisablePreviewInteraction();
        mediaPlayer.Pause();
        completed = true;
        _ = completion.TrySetResult(true);
    }

    private void Dismiss()
    {
        if (completed || transitioning)
        {
            return;
        }

        transitioning = true;
        StopEntrance();
        DisablePreviewInteraction();
        mediaPlayer.Pause();
        PlayDismissAnimation();
        dismissTimer = root.DispatcherQueue.CreateTimer();
        dismissTimer.Interval = TimeSpan.FromMilliseconds(DismissDurationMs);
        dismissTimer.IsRepeating = false;
        dismissTimer.Tick += HandleDismissCompleted;
        dismissTimer.Start();
    }

    private void HandleDismissCompleted(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= HandleDismissCompleted;
        dismissTimer = null;
        completed = true;
        _ = completion.TrySetResult(false);
        CloseCore();
    }

    private void PlayDismissAnimation()
    {
        Border activePreviewHost = interactivePreviewHost ?? previewHost;
        Visual previewVisual = ElementCompositionPreview.GetElementVisual(activePreviewHost);
        Visual toolbarVisual = ElementCompositionPreview.GetElementVisual(toolbar);
        Visual backdropVisual = ElementCompositionPreview.GetElementVisual(reviewBackdrop);
        Compositor compositor = previewVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(DismissDurationMs);
        SineEasingFunction easing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.In);
        float distance = (float)Math.Max(120, desktopBounds.Height - previewBounds.Y + 48);
        AnimateDown(previewVisual, distance, duration, easing);
        AnimateDown(toolbarVisual, distance, duration, easing);
        backdropVisual.Opacity = 0;
        backdropVisual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(compositor, 1, 0, duration, easing));
    }

    private void PlayFlight(NativeRectangle landingBounds, Action onArrived, TaskCompletionSource completionSource)
    {
        if (closed || flightInProgress)
        {
            throw new InvalidOperationException("The recording review is unavailable for handoff.");
        }

        flightInProgress = true;
        playerElement.AreTransportControlsEnabled = false;
        playerElement.IsHitTestVisible = false;
        mediaPlayer.Pause();
        Rect sourceBounds = previewBounds;
        Rect targetBounds = ToLocal(landingBounds);
        Image flightPreview = new()
        {
            Source = previewImage,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        Border captureSurface = CreatePreviewHost(flightPreview, sourceBounds.Width, sourceBounds.Height);
        captureSurface.BorderThickness = new Thickness(0);
        captureSurface.IsHitTestVisible = false;
        captureSurface.Translation = new Vector3(0, 0, 40);
        ElementCompositionPreview.SetIsTranslationEnabled(captureSurface, true);
        Canvas.SetLeft(captureSurface, sourceBounds.X);
        Canvas.SetTop(captureSurface, sourceBounds.Y);

        Canvas flightCanvas = new() { IsHitTestVisible = false };
        flightCanvas.Children.Add(captureSurface);
        root.KeyDown -= HandleKeyDown;
        root.Children.Clear();
        root.Background = null;
        root.Children.Add(flightCanvas);
        root.UpdateLayout();

        CompositionScopedBatch? animationBatch = null;
        DispatcherQueueTimer? fallbackTimer = null;
        EventHandler<object>? renderingHandler = null;
        bool finished = false;

        void Finish(Exception? exception = null)
        {
            if (finished)
            {
                return;
            }

            finished = true;

            if (renderingHandler is not null)
            {
                CompositionTarget.Rendering -= renderingHandler;
            }

            if (fallbackTimer is not null)
            {
                fallbackTimer.Stop();
                fallbackTimer.Tick -= HandleFallback;
                fallbackTimer = null;
            }

            animationBatch?.Dispose();
            animationBatch = null;

            if (exception is null)
            {
                try
                {
                    onArrived();
                }
                catch (Exception arrivalException)
                {
                    exception = arrivalException;
                }
            }

            CloseCore();

            if (exception is null)
            {
                _ = completionSource.TrySetResult();
            }
            else
            {
                _ = completionSource.TrySetException(exception);
            }
        }

        void HandleFallback(DispatcherQueueTimer sender, object args) => Finish();

        int preparationFrames = 0;
        renderingHandler = (_, _) =>
        {
            if (++preparationFrames < 2)
            {
                return;
            }

            CompositionTarget.Rendering -= renderingHandler;
            renderingHandler = null;

            try
            {
                _ = DwmFlush();
                animationBatch = StartFlightAnimation(captureSurface, sourceBounds, targetBounds);
                animationBatch.Completed += (_, _) => Finish();
                fallbackTimer = root.DispatcherQueue.CreateTimer();
                fallbackTimer.Interval = TimeSpan.FromMilliseconds(FlightAnimationDurationMs + 120);
                fallbackTimer.IsRepeating = false;
                fallbackTimer.Tick += HandleFallback;
                fallbackTimer.Start();
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        };

        CompositionTarget.Rendering += renderingHandler;
    }

    private static CompositionScopedBatch StartFlightAnimation(Border captureSurface, Rect sourceBounds, Rect targetBounds)
    {
        Visual captureVisual = ElementCompositionPreview.GetElementVisual(captureSurface);
        Compositor compositor = captureVisual.Compositor;
        TimeSpan duration = TimeSpan.FromMilliseconds(FlightAnimationDurationMs);
        SineEasingFunction captureEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.InOut);
        SineEasingFunction flightEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.Out);
        SineEasingFunction fadeEasing = CompositionEasingFunction.CreateSineEasingFunction(compositor, CompositionEasingFunctionMode.InOut);
        float captureBeatProgress = CaptureBeatDurationMs / (float)FlightAnimationDurationMs;
        float flightStartProgress = (CaptureBeatDurationMs + CaptureHoldDurationMs) / (float)FlightAnimationDurationMs;
        float fadeStartProgress = (FlightAnimationDurationMs - CaptureBeatDurationMs) / (float)FlightAnimationDurationMs;
        Vector3 sourceOffset = captureVisual.Offset;
        Vector3 sourceCenter = new((float)sourceBounds.Width / 2, (float)sourceBounds.Height / 2, 0);
        Vector3 targetCenter = new((float)(targetBounds.X + (targetBounds.Width / 2)), (float)(targetBounds.Y + (targetBounds.Height / 2)), 0);
        Vector3 targetOffset = targetCenter - sourceCenter;
        float targetScale = Math.Min(1, Math.Min(64f / Math.Max(1, (float)sourceBounds.Width), 40f / Math.Max(1, (float)sourceBounds.Height)));
        Vector3 capturedScale = new(0.965f, 0.965f, 1);
        Vector3 finalScale = new(targetScale, targetScale, 1);
        captureVisual.CenterPoint = sourceCenter;

        Vector3KeyFrameAnimation offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = duration;
        offsetAnimation.InsertKeyFrame(0, sourceOffset);
        offsetAnimation.InsertKeyFrame(flightStartProgress, sourceOffset);
        offsetAnimation.InsertKeyFrame(1, targetOffset, flightEasing);

        Vector3KeyFrameAnimation scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.Duration = duration;
        scaleAnimation.InsertKeyFrame(0, Vector3.One);
        scaleAnimation.InsertKeyFrame(captureBeatProgress, capturedScale, captureEasing);
        scaleAnimation.InsertKeyFrame(flightStartProgress, capturedScale);
        scaleAnimation.InsertKeyFrame(1, finalScale, flightEasing);

        ScalarKeyFrameAnimation opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Duration = duration;
        opacityAnimation.InsertKeyFrame(0, 1);
        opacityAnimation.InsertKeyFrame(fadeStartProgress, 1);
        opacityAnimation.InsertKeyFrame(1, 0, fadeEasing);

        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        captureVisual.Offset = targetOffset;
        captureVisual.Scale = finalScale;
        captureVisual.Opacity = 0;
        captureVisual.StartAnimation(nameof(Visual.Offset), offsetAnimation);
        captureVisual.StartAnimation(nameof(Visual.Scale), scaleAnimation);
        captureVisual.StartAnimation(nameof(Visual.Opacity), opacityAnimation);
        batch.End();
        return batch;
    }

    private void StopEntrance()
    {
        if (entranceRenderingHandler is not null)
        {
            CompositionTarget.Rendering -= entranceRenderingHandler;
            entranceRenderingHandler = null;
        }

        if (entranceTimer is not null)
        {
            entranceTimer.Stop();
            entranceTimer.Tick -= HandleEntranceCompleted;
            entranceTimer = null;
        }

        Visual previewVisual = ElementCompositionPreview.GetElementVisual(previewHost);
        previewVisual.Opacity = 1;
    }

    private void DisablePreviewInteraction()
    {
        reviewCanvas.IsHitTestVisible = false;
        previewHost.IsHitTestVisible = false;
        _ = (interactivePreviewHost?.IsHitTestVisible = false);
        playerElement.IsHitTestVisible = false;
    }

    private void HandleClosed(object sender, WindowEventArgs args)
    {
        closed = true;
        _ = completion.TrySetResult(false);
        mediaPlayer.Dispose();
    }

    private void CloseCore()
    {
        if (closed)
        {
            return;
        }

        closed = true;
        StopEntrance();
        dismissTimer?.Stop();
        root.KeyDown -= HandleKeyDown;
        window.Closed -= HandleClosed;
        mediaPlayer.Dispose();

        try
        {
            PlatformWindowExtensions.viSetOpacity(windowHandle, 0);
            window.AppWindow.Hide();
            window.Close();
        }
        catch (COMException)
        {
        }
    }

    private Rect ToLocal(NativeRectangle bounds)
    {
        double scaleX = root.ActualWidth / desktopBounds.Width;
        double scaleY = root.ActualHeight / desktopBounds.Height;
        return new Rect((bounds.Left - desktopBounds.Left) * scaleX,
            (bounds.Top - desktopBounds.Top) * scaleY,
            bounds.Width * scaleX,
            bounds.Height * scaleY);
    }

    private static void AnimateDown(Visual visual, float distance, TimeSpan duration, CompositionEasingFunction easing)
    {
        Vector3 offset = visual.Offset;
        Vector3 destination = offset + new Vector3(0, distance, 0);
        visual.Offset = destination;
        visual.Opacity = 0;
        visual.StartAnimation(nameof(Visual.Offset), CreateVectorAnimation(visual.Compositor, offset, destination, duration, easing));
        visual.StartAnimation(nameof(Visual.Opacity), CreateScalarAnimation(visual.Compositor, 1, 0, duration, easing, 0.55f));
    }

    private static Border CreatePreviewHost(UIElement content, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 20, 28)),
        BorderBrush = ResolveBrush("ControlStrokeColorDefaultBrush", Windows.UI.Color.FromArgb(72, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
        Shadow = new ThemeShadow()
    };

    private static Button CreateToolbarButton(string glyph, string label, bool accent) => CreateToolbarButton(new FontIcon
    {
        FontFamily = new FontFamily("Segoe Fluent Icons"),
        FontSize = 14,
        Glyph = glyph
    }, label, accent);

    private static Button CreateToolbarButton(UIElement content, string label, bool accent)
    {
        Button button = new()
        {
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            Content = content
        };

        if (accent && Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentButtonStyle", out object value) && value is Style style)
        {
            button.Style = style;
        }

        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
        return button;
    }

    private static Vector3KeyFrameAnimation CreateVectorAnimation(Compositor compositor,
        Vector3 from,
        Vector3 to,
        TimeSpan duration,
        CompositionEasingFunction easing)
    {
        Vector3KeyFrameAnimation animation = compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        return animation;
    }

    private static ScalarKeyFrameAnimation CreateScalarAnimation(Compositor compositor,
        float from,
        float to,
        TimeSpan duration,
        CompositionEasingFunction easing,
        float delayProgress = 0)
    {
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);

        if (delayProgress > 0)
        {
            animation.InsertKeyFrame(delayProgress, from);
        }

        animation.InsertKeyFrame(1, to, easing);
        return animation;
    }

    private static Brush ResolveBrush(string key, Windows.UI.Color fallback) => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(fallback);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(nint window, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint window, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint window, int index, int newValue);
}
