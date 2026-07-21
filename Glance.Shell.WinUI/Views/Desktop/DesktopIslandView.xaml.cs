using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
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

public partial class DesktopIslandView :
    DesktopIsland
{
    private readonly DispatcherQueue dispatcherQueue;
    private int previousIndex;
    private bool skipNextConnectedExpansion;

    public DesktopIslandView()
    {
        InitializeComponent();
        dispatcherQueue = DispatcherQueue;

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
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

    private void HandleLoaded(object sender, RoutedEventArgs args)
    {
        previousIndex = ViewModel.SelectedIndex;
        ViewModel.PropertyChanged += HandleViewModelPropertyChanged;
        ViewModel.AttentionReceived += HandleAttentionReceived;
    }

    private void HandleUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        ViewModel.AttentionReceived -= HandleAttentionReceived;
    }

    private void HandleAttentionReceived(object? sender, GlanceAttentionRequest request) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            FrameworkElement presenter = ViewModel.IsExpanded
                ? ExpandedPresenter
                : CompactPresenter;

            FluentMotion.PlayPulse(presenter);
        });

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
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
                // The destination can detach if the island changes state again mid-transition.
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
        if (!CanAcceptContent(args.DataView))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
        DispatcherQueue.TryEnqueue(() =>
            ViewModel.TryActivateContent(GlanceContentKind.FilesAndFolders));
    }

    private void HandleDragOver(object sender, DragEventArgs args)
    {
        if (!CanAcceptContent(args.DataView))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    private bool CanAcceptContent(DataPackageView dataView)
    {
        try
        {
            return dataView.Contains(StandardDataFormats.StorageItems) &&
                ViewModel.CanHandleContent(GlanceContentKind.FilesAndFolders);
        }
        catch (COMException)
        {
            return false;
        }
    }

    private async void HandleDrop(object sender, DragEventArgs args)
    {
        DragOperationDeferral deferral = args.GetDeferral();
        GlanceStorageItem[] items = [];

        try
        {
            DataPackageView dataView = args.DataView;

            if (!dataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            IReadOnlyList<IStorageItem> storageItems =
                await dataView.GetStorageItemsAsync();
            items = storageItems
                .Select(CreateStorageItem).OfType<GlanceStorageItem>().ToArray();
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

        if (items.Length > 0)
        {
            try
            {
                await ProcessStorageItemsAsync(items);
            }
            catch (Exception)
            {
            }
        }
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

    private Task ProcessStorageItemsAsync(IReadOnlyList<GlanceStorageItem> items)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            return AddStorageItemsAsync(items);
        }

        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await AddStorageItemsAsync(items);
                completion.TrySetResult(true);
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

    private async Task AddStorageItemsAsync(IReadOnlyList<GlanceStorageItem> items)
    {
        if (items.Count > 0)
        {
            await ViewModel.HandleContentAsync(new GlanceContentContext(GlanceContentKind.FilesAndFolders, items));
        }
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
