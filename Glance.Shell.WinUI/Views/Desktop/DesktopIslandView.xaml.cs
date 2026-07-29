using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.Shell.WinUI;

public sealed partial class DesktopIslandView :
    DesktopIsland
{
    private const int AttentionExpansionDurationMs = 4000;
    private const int ContextualDragExitDelayMs = 160;
    private const int InteractionExitDelayMs = 240;
    private const int StartupAttentionDelayMs = 2500;

    private readonly DispatcherQueue dispatcherQueue;
    private DispatcherQueueTimer? attentionExpansionTimer;
    private DispatcherQueueTimer? contextualDragExitTimer;
    private DispatcherQueueTimer? interactionExitTimer;
    private DispatcherQueueTimer? startupAttentionTimer;
    private Button? pressedButton;
    private bool isContextualDragActive;
    private int contextualDragSession;
    private IGlanceExpansionLockComponent? expansionLockComponent;
    private IGlanceInteractionAwareComponent? interactionComponent;
    private bool isPointerOverIsland;
    private int previousIndex;
    private bool skipNextConnectedExpansion;

    public DesktopIslandView()
    {
        InitializeComponent();
        dispatcherQueue = DispatcherQueue;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        AddHandler(PointerPressedEvent, new PointerEventHandler(HandleButtonPointerPressed), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler(HandleButtonPointerReleased), true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(HandleButtonPointerCanceled), true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(HandleButtonPointerCaptureLost), true);
    }

    public DesktopIslandViewModel ViewModel => (DesktopIslandViewModel)DataContext;

    public DesktopIslandPlacement ToPlacement(int index)
    {
        GlancePlacement placement = (GlancePlacement)index;

        return placement switch
        {
            GlancePlacement.Top => DesktopIslandPlacement.Top,
            GlancePlacement.Bottom => DesktopIslandPlacement.Bottom,
            _ => DesktopIslandPlacement.Top
        };
    }

    public Visibility WhenPinned(bool isPinned) =>
        isPinned ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WhenNotPinned(bool isPinned) =>
        isPinned ? Visibility.Collapsed : Visibility.Visible;

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        previousIndex = ViewModel.SelectedIndex;
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ViewModel.AttentionReceived += HandleAttentionReceived;
        Deactivated += HandleIslandDeactivated;
        DispatcherQueue.TryEnqueue(InitializeExpansionState);
        StartStartupAttentionTimer();
    }

    private void InitializeExpansionState()
    {
        ViewModel.IsExpanded = ViewModel.IsPinned;
        UpdateExpansionLockComponent();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ViewModel.AttentionReceived -= HandleAttentionReceived;
        Deactivated -= HandleIslandDeactivated;
        ReleasePressedButton();
        ClearExpansionLockComponent();
        EndComponentInteraction();
        StopAttentionExpansionTimer();
        StopContextualDragExitTimer();
        StopInteractionExitTimer();
        StopStartupAttentionTimer();
    }

    private void StartStartupAttentionTimer()
    {
        startupAttentionTimer ??= CreateStartupAttentionTimer();
        startupAttentionTimer.Stop();
        startupAttentionTimer.Start();
    }

    private DispatcherQueueTimer CreateStartupAttentionTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(StartupAttentionDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleStartupAttentionTimerTick;
        return timer;
    }

    private void StopStartupAttentionTimer() => startupAttentionTimer?.Stop();

    private void HandleStartupAttentionTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        ViewModel.CompleteStartup();
    }

    private void HandleAttentionReceived(object? sender, GlanceAttentionRequest request) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            Reveal();

            if (request.Expand)
            {
                StartAttentionExpansionTimer();
            }

            FrameworkElement presenter = ViewModel.IsExpanded
                ? ExpandedPresenter
                : CompactPresenter;

            FluentMotion.PlayPulse(presenter);
        });

    private void StartAttentionExpansionTimer()
    {
        attentionExpansionTimer ??= CreateAttentionExpansionTimer();
        attentionExpansionTimer.Stop();
        attentionExpansionTimer.Start();
    }

    private DispatcherQueueTimer CreateAttentionExpansionTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(AttentionExpansionDurationMs);
        timer.IsRepeating = false;
        timer.Tick += HandleAttentionExpansionTimerTick;
        return timer;
    }

    private void StopAttentionExpansionTimer() => attentionExpansionTimer?.Stop();

    private void HandleAttentionExpansionTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (isPointerOverIsland)
        {
            return;
        }

        if (IsPointerWithinInteractiveRegion)
        {
            isPointerOverIsland = true;
            UpdateComponentInteraction();
            return;
        }

        if (!isContextualDragActive)
        {
            Dismiss();
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DesktopIslandViewModel.SelectedComponent))
        {
            UpdateExpansionLockComponent();
            UpdateComponentInteraction();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsPinned))
        {
            ApplyExpansionLock();
            return;
        }

        if (args.PropertyName == nameof(DesktopIslandViewModel.IsExpanded))
        {
            if (skipNextConnectedExpansion)
            {
                skipNextConnectedExpansion = false;
                return;
            }

            PlayConnectedExpansionAnimation();
            return;
        }

        if (args.PropertyName != nameof(DesktopIslandViewModel.SelectedIndex))
        {
            return;
        }

        int selectedIndex = ViewModel.SelectedIndex;
        int direction = selectedIndex > previousIndex ? 1 : -1;
        skipNextConnectedExpansion = true;

        if (previousIndex == ViewModel.ComponentCount - 1 && selectedIndex == 0)
        {
            direction = 1;
        }
        else if (previousIndex == 0 && selectedIndex == ViewModel.ComponentCount - 1)
        {
            direction = -1;
        }

        previousIndex = selectedIndex;

        DispatcherQueue.TryEnqueue(() =>
        {
            skipNextConnectedExpansion = false;
            FluentMotion.PlayHorizontalPageTransition(CompactPresenter, direction);
            FluentMotion.PlayHorizontalPageTransition(ExpandedPresenter, direction);
        });
    }

    private void HandleIslandPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        StopAttentionExpansionTimer();
        StopInteractionExitTimer();
        isPointerOverIsland = true;
        UpdateComponentInteraction();
        Reveal();
        ViewModel.IsExpanded = true;
    }

    private void HandleIslandPointerExited(object sender, PointerRoutedEventArgs args)
    {
        isPointerOverIsland = false;
        ScheduleInteractionExit();
    }

    private void HandleButtonPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        Button? button = FindButton(args.OriginalSource as DependencyObject);

        if (button is null || !button.IsEnabled)
        {
            return;
        }

        if (!ReferenceEquals(pressedButton, button))
        {
            ReleasePressedButton();
            pressedButton = button;
        }

        FluentMotion.PlayButtonPress(button);
    }

    private void HandleButtonPointerReleased(object sender, PointerRoutedEventArgs args) =>
        ReleasePressedButton();

    private void HandleButtonPointerCanceled(object sender, PointerRoutedEventArgs args) =>
        ReleasePressedButton();

    private void HandleButtonPointerCaptureLost(object sender, PointerRoutedEventArgs args) =>
        ReleasePressedButton();

    private void ReleasePressedButton()
    {
        Button? button = pressedButton;
        pressedButton = null;

        if (button is not null)
        {
            FluentMotion.PlayButtonRelease(button);
        }
    }

    private Button? FindButton(DependencyObject? element)
    {
        while (element is not null && !ReferenceEquals(element, this))
        {
            if (element is Button button)
            {
                return button;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private void ScheduleInteractionExit()
    {
        interactionExitTimer ??= CreateInteractionExitTimer();
        interactionExitTimer.Stop();
        interactionExitTimer.Start();
    }

    private DispatcherQueueTimer CreateInteractionExitTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(InteractionExitDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleInteractionExitTimerTick;
        return timer;
    }

    private void StopInteractionExitTimer() => interactionExitTimer?.Stop();

    private void HandleInteractionExitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();

        if (isPointerOverIsland)
        {
            return;
        }

        if (IsPointerWithinInteractiveRegion)
        {
            isPointerOverIsland = true;
            UpdateComponentInteraction();
        }
        else
        {
            EndComponentInteraction();
        }
    }

    private void UpdateComponentInteraction()
    {
        IGlanceInteractionAwareComponent? selectedComponent = ViewModel.SelectedComponent as IGlanceInteractionAwareComponent;

        if (ReferenceEquals(interactionComponent, selectedComponent))
        {
            return;
        }

        EndComponentInteraction();

        if (isPointerOverIsland && selectedComponent is not null)
        {
            interactionComponent = selectedComponent;
            interactionComponent.BeginInteraction();
        }
    }

    private void EndComponentInteraction()
    {
        IGlanceInteractionAwareComponent? previousComponent = interactionComponent;
        interactionComponent = null;
        previousComponent?.EndInteraction();
    }

    private void UpdateExpansionLockComponent()
    {
        IGlanceExpansionLockComponent? selectedComponent = ViewModel.SelectedComponent as IGlanceExpansionLockComponent;

        if (ReferenceEquals(expansionLockComponent, selectedComponent))
        {
            ApplyExpansionLock();
            return;
        }

        ClearExpansionLockComponent();
        expansionLockComponent = selectedComponent;

        if (expansionLockComponent is not null)
        {
            expansionLockComponent.ExpansionLockChanged += HandleExpansionLockChanged;
        }

        ApplyExpansionLock();
    }

    private void ClearExpansionLockComponent()
    {
        if (expansionLockComponent is not null)
        {
            expansionLockComponent.ExpansionLockChanged -= HandleExpansionLockChanged;
            expansionLockComponent = null;
        }

        IsExpansionLocked = ViewModel.IsPinned;
    }

    private void HandleExpansionLockChanged(object? sender, EventArgs args) =>
        DispatcherQueue.TryEnqueue(ApplyExpansionLock);

    private void ApplyExpansionLock() =>
        IsExpansionLocked = ViewModel.IsPinned || expansionLockComponent?.IsExpansionLocked == true;

    private void HandleIslandDeactivated(object? sender, EventArgs args)
    {
        if (expansionLockComponent?.IsExpansionLocked == true)
        {
            expansionLockComponent.DismissExpansionLock();
        }
    }

    private void PlayConnectedExpansionAnimation()
    {
        IGlanceComponent? selectedComponent = ViewModel.SelectedComponent;

        if (selectedComponent is not IGlanceConnectedAnimationComponent component)
        {
            return;
        }

        object sourceElement = ViewModel.IsExpanded
            ? component.CompactAnimationElement
            : component.ExpandedAnimationElement;
        object destinationElement = ViewModel.IsExpanded
            ? component.ExpandedAnimationElement
            : component.CompactAnimationElement;

        if (sourceElement is not FrameworkElement source ||
            destinationElement is not FrameworkElement destination ||
            !IsInElementTree(source))
        {
            return;
        }

        ConnectedAnimationService animationService =
            ConnectedAnimationService.GetForCurrentView();
        string animationKey = $"DesktopIsland.{selectedComponent.Id}.Status";

        try
        {
            animationService.PrepareToAnimate(animationKey, source);
        }
        catch (ArgumentException)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            ConnectedAnimation? animation = animationService.GetAnimation(animationKey);

            if (animation is null || !IsInElementTree(destination))
            {
                return;
            }

            animation.Configuration = new DirectConnectedAnimationConfiguration();

            try
            {
                animation.TryStart(destination);
            }
            catch (ArgumentException)
            {
            }
        });
    }

    private static bool IsInElementTree(FrameworkElement element) =>
        element.IsLoaded && element.XamlRoot is not null;

    private void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        int delta = args.GetCurrentPoint(this).Properties.MouseWheelDelta;

        if (delta != 0)
        {
            ViewModel.Move(delta < 0 ? 1 : -1);
            args.Handled = true;
        }
    }

    private void HandleDragEnter(object sender, DragEventArgs args)
    {
        if (!TryGetContentKind(args.DataView, out GlanceContentKind kind))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        isContextualDragActive = true;
        int session = ++contextualDragSession;
        args.AcceptedOperation = DataPackageOperation.Copy;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (isContextualDragActive && session == contextualDragSession)
            {
                ViewModel.TryActivateContent(kind);
            }
        });
    }

    private void HandleDragOver(object sender, DragEventArgs args)
    {
        if (!TryGetContentKind(args.DataView, out _))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            ScheduleContextualDragExit();
            return;
        }

        StopContextualDragExitTimer();
        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void HandleDragLeave(object sender, DragEventArgs args) =>
        ScheduleContextualDragExit();

    private bool TryGetContentKind(DataPackageView dataView,
        out GlanceContentKind kind)
    {
        try
        {
            if (dataView.Contains(StandardDataFormats.StorageItems))
            {
                if (ViewModel.CanHandleContent(GlanceContentKind.FilesAndFolders))
                {
                    kind = GlanceContentKind.FilesAndFolders;
                    return true;
                }

                kind = default;
                return false;
            }

            if ((dataView.Contains(StandardDataFormats.WebLink) ||
                dataView.Contains(StandardDataFormats.ApplicationLink)) &&
                ViewModel.CanHandleContent(GlanceContentKind.WebLink))
            {
                kind = GlanceContentKind.WebLink;
                return true;
            }

            if (dataView.Contains(StandardDataFormats.Text) &&
                ViewModel.CanHandleContent(GlanceContentKind.Text))
            {
                kind = GlanceContentKind.Text;
                return true;
            }
        }
        catch (COMException)
        {
        }

        kind = default;
        return false;
    }

    private async void HandleDrop(object sender, DragEventArgs args)
    {
        StopContextualDragExitTimer();
        DragOperationDeferral deferral = args.GetDeferral();
        GlanceContentContext? context = null;
        bool contentHandled = false;

        try
        {
            DataPackageView dataView = args.DataView;

            if (TryGetContentKind(dataView, out GlanceContentKind kind))
            {
                context = await CreateContentContextAsync(dataView, kind);
            }
        }
        catch (COMException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            await CompleteDropDeferralAsync(deferral);
        }

        if (context is not null)
        {
            try
            {
                contentHandled = await ProcessContentAsync(context);
            }
            catch (Exception)
            {
            }
        }

        CompleteContextualDrag(contentHandled);
    }

    private void ScheduleContextualDragExit()
    {
        if (!isContextualDragActive)
        {
            return;
        }

        contextualDragExitTimer ??= CreateContextualDragExitTimer();
        contextualDragExitTimer.Stop();
        contextualDragExitTimer.Start();
    }

    private DispatcherQueueTimer CreateContextualDragExitTimer()
    {
        DispatcherQueueTimer timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(ContextualDragExitDelayMs);
        timer.IsRepeating = false;
        timer.Tick += HandleContextualDragExitTimerTick;
        return timer;
    }

    private void StopContextualDragExitTimer() => contextualDragExitTimer?.Stop();

    private void HandleContextualDragExitTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        CompleteContextualDrag(false);
    }

    private void CompleteContextualDrag(bool contentHandled)
    {
        if (!dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(() => CompleteContextualDrag(contentHandled));
            return;
        }

        StopContextualDragExitTimer();
        isContextualDragActive = false;
        contextualDragSession++;

        if (contentHandled)
        {
            Reveal();
            return;
        }

        ViewModel.EndContentPreview();
        Dismiss();
    }

    private Task CompleteDropDeferralAsync(DragOperationDeferral deferral)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            CompleteDropDeferral(deferral);
            return Task.CompletedTask;
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!dispatcherQueue.TryEnqueue(() =>
        {
            CompleteDropDeferral(deferral);
            completion.TrySetResult(true);
        }))
        {
            completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private static void CompleteDropDeferral(DragOperationDeferral deferral)
    {
        try
        {
            deferral.Complete();
        }
        catch (COMException)
        {
        }
    }

    private Task<bool> ProcessContentAsync(GlanceContentContext context)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            return ViewModel.HandleContentAsync(context);
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                completion.TrySetResult(await ViewModel.HandleContentAsync(context));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private static async Task<GlanceContentContext?> CreateContentContextAsync(DataPackageView dataView,
        GlanceContentKind kind)
    {
        if (kind == GlanceContentKind.FilesAndFolders)
        {
            IReadOnlyList<IStorageItem> storageItems = await dataView.GetStorageItemsAsync();
            GlanceStorageItem[] items = [.. storageItems.Select(CreateStorageItem).OfType<GlanceStorageItem>()];
            return items.Length == 0 ? null : new GlanceContentContext(kind, items);
        }

        if (kind == GlanceContentKind.WebLink)
        {
            Uri? uri = dataView.Contains(StandardDataFormats.WebLink)
                ? await dataView.GetWebLinkAsync()
                : await dataView.GetApplicationLinkAsync();
            return uri is null ? null : new GlanceContentContext(kind, [], uri.AbsoluteUri);
        }

        string text = await dataView.GetTextAsync();
        return string.IsNullOrWhiteSpace(text) ? null : new GlanceContentContext(kind, [], text);
    }

    private static GlanceStorageItem? CreateStorageItem(IStorageItem storageItem)
    {
        try
        {
            string path = storageItem.Path;

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(normalizedPath);

            return new GlanceStorageItem(path, string.IsNullOrWhiteSpace(name) ? storageItem.Name : name, storageItem is StorageFolder);
        }
        catch (COMException)
        {
            return null;
        }
    }
}
