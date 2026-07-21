using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Glance.RemovableDevices.WinUI;

public sealed class RemovableDevicesComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ITextLocalizer localizer;
    private readonly IRemovableDeviceService removableDeviceService;
    private readonly DispatcherQueueTimer timer;
    private readonly RemovableDevicesViewModel viewModel;
    private HashSet<string> currentDeviceIds = new(StringComparer.OrdinalIgnoreCase);
    private bool hasSnapshot;
    private int isRefreshing;

    public RemovableDevicesComponent(
        RemovableDevicesViewModel viewModel,
        IRemovableDeviceService removableDeviceService,
        IGlanceAttentionService attentionService,
        ModuleResourceTextLocalizer<RemovableDevicesModule> localizer)
    {
        this.viewModel = viewModel;
        this.removableDeviceService = removableDeviceService;
        this.attentionService = attentionService;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        RemovableDevicesCompactView compactView = new(viewModel);
        RemovableDevicesExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        timer = dispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(2);
        timer.IsRepeating = true;
        timer.Tick += HandleTick;
        timer.Start();

        viewModel.OpenRequested += HandleOpenRequested;
        viewModel.EjectRequested += HandleEjectRequested;
        _ = RefreshAsync();
    }

    public string Id => "RemovableDevices";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 130;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= HandleTick;
        viewModel.OpenRequested -= HandleOpenRequested;
        viewModel.EjectRequested -= HandleEjectRequested;
    }

    private void HandleTick(DispatcherQueueTimer sender, object args) =>
        _ = RefreshAsync();

    private void HandleOpenRequested(object? sender, RemovableDevice device) =>
        _ = OpenAsync(device);

    private void HandleEjectRequested(object? sender, RemovableDevice device)
    {
        viewModel.SetBusy(device.Id, true);
        _ = EjectAsync(device);
    }

    private async Task OpenAsync(RemovableDevice device)
    {
        bool opened = await Task.Run(() => removableDeviceService.TryOpen(device));

        if (!opened)
        {
            dispatcherQueue.TryEnqueue(() => viewModel.ShowOpenFailure(device.Id));
        }
    }

    private async Task EjectAsync(RemovableDevice device)
    {
        bool ejected = await Task.Run(() => removableDeviceService.TryEject(device));

        if (!ejected)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                viewModel.SetBusy(device.Id, false);
                viewModel.ShowEjectFailure(device.Id);
            });
            return;
        }

        await Task.Delay(500);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref isRefreshing, 1) != 0)
        {
            return;
        }

        IReadOnlyList<RemovableDevice> devices;

        try
        {
            devices = await Task.Run(removableDeviceService.GetDevices);
        }
        catch (Exception)
        {
            devices = [];
        }
        finally
        {
            Interlocked.Exchange(ref isRefreshing, 0);
        }

        dispatcherQueue.TryEnqueue(() => ApplyDevices(devices));
    }

    private void ApplyDevices(IReadOnlyList<RemovableDevice> devices)
    {
        RemovableDevice? addedDevice = hasSnapshot
            ? devices.FirstOrDefault(device => !currentDeviceIds.Contains(device.Id))
            : null;

        viewModel.Update(devices, addedDevice?.Id);
        currentDeviceIds = devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (addedDevice is not null)
        {
            attentionService.RequestAttention(Id);
        }

        hasSnapshot = true;
    }
}
