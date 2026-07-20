using Elysium.UI.Controls.WinUI;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Glance.Shell.WinUI;

public partial class DesktopIslandView : 
    DesktopIsland
{
    private int previousIndex;

    public DesktopIslandView()
    {
        InitializeComponent();

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    public DesktopIslandViewModel ViewModel => (DesktopIslandViewModel)DataContext;

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
            PlayConnectedExpansionAnimation();
            return;
        }

        if (args.PropertyName != nameof(DesktopIslandViewModel.SelectedIndex))
        {
            return;
        }

        int selectedIndex = ViewModel.SelectedIndex;
        int direction = selectedIndex > previousIndex ? 1 : -1;

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
            destinationElement is not FrameworkElement destination)
        {
            return;
        }

        ConnectedAnimationService animationService =
            ConnectedAnimationService.GetForCurrentView();
        string animationKey = $"DesktopIsland.{selectedComponent.Id}.Status";

        animationService.PrepareToAnimate(animationKey, source);

        DispatcherQueue.TryEnqueue(() =>
        {
            ConnectedAnimation? animation = animationService.GetAnimation(animationKey);

            if (animation is null)
            {
                return;
            }

            animation.Configuration = new DirectConnectedAnimationConfiguration();
            animation.TryStart(destination);
        });
    }

    private void HandlePointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        int delta = args.GetCurrentPoint(this).Properties.MouseWheelDelta;

        if (delta != 0)
        {
            ViewModel.Move(delta < 0 ? 1 : -1);
            args.Handled = true;
        }
    }

    private void HandleDragEnter(object sender, DragEventArgs args) =>
        HandleDragOver(sender, args);

    private void HandleDragOver(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems) ||
            !ViewModel.TryActivateContent(GlanceContentKind.FilesAndFolders))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void HandleDrop(object sender, DragEventArgs args)
    {
        if (!args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = args.GetDeferral();

        try
        {
            IReadOnlyList<IStorageItem> storageItems =
                await args.DataView.GetStorageItemsAsync();
            GlanceStorageItem[] items = storageItems
                .Select(CreateStorageItem)
                .OfType<GlanceStorageItem>()
                .ToArray();

            if (items.Length > 0)
            {
                await ViewModel.HandleContentAsync(new GlanceContentContext(
                    GlanceContentKind.FilesAndFolders,
                    items));
            }
        }
        catch (COMException)
        {
            // Explorer can withdraw a projected item while the drag is completing.
        }
        catch (Exception)
        {
            // Unsupported virtual shell items are ignored without destabilizing the island.
        }
        finally
        {
            try
            {
                deferral.Complete();
            }
            catch (COMException)
            {
                // The native drag operation may already have completed or been cancelled.
            }
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

            string normalizedPath = path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(normalizedPath);

            return new GlanceStorageItem(
                path,
                string.IsNullOrWhiteSpace(name) ? storageItem.Name : name,
                storageItem is StorageFolder);
        }
        catch (COMException)
        {
            return null;
        }
    }
}
