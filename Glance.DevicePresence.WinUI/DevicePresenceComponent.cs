using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;

namespace Glance.DevicePresence.WinUI;

public sealed class DevicePresenceComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly DevicePresenceAttentionTracker attentionTracker = new();
    private readonly IDevicePresenceService devicePresenceService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ModuleResourceTextLocalizer<DevicePresenceModule> localizer;
    private readonly DevicePresenceViewModel viewModel;
    private readonly GlanceModuleOptions<DevicePresenceSettings> options;

    public DevicePresenceComponent(
        DevicePresenceViewModel viewModel,
        IDevicePresenceService devicePresenceService,
        IGlanceAttentionService attentionService,
        GlanceModuleOptions<DevicePresenceSettings> options,
        ModuleResourceTextLocalizer<DevicePresenceModule> localizer)
    {
        this.viewModel = viewModel;
        this.devicePresenceService = devicePresenceService;
        this.attentionService = attentionService;
        this.options = options;
        this.localizer = localizer;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        DevicePresenceCompactView compactView = new(viewModel);
        DevicePresenceExpandedView expandedView = new(viewModel, localizer);

        CompactContent = compactView;
        ExpandedContent = expandedView;
        CompactAnimationElement = compactView.ConnectedAnimationElement;
        ExpandedAnimationElement = expandedView.ConnectedAnimationElement;

        devicePresenceService.DevicesChanged += HandleDevicesChanged;
        options.Changed += HandleOptionsChanged;

        if (devicePresenceService.IsReady)
        {
            ApplyDevices(devicePresenceService.GetConnectedDevices());
        }
    }

    public string Id => "DevicePresence";

    public string DisplayName => localizer.GetText("ModuleDisplayName");

    public string Description => localizer.GetText("ModuleDescription");

    public int Order => 140;

    public object CompactContent { get; }

    public object ExpandedContent { get; }

    public object CompactAnimationElement { get; }

    public object ExpandedAnimationElement { get; }

    public void Dispose()
    {
        devicePresenceService.DevicesChanged -= HandleDevicesChanged;
        options.Changed -= HandleOptionsChanged;
    }

    private byte LowBatteryThreshold => (byte)Math.Clamp(options.Current.LowBatteryThreshold, 5, 50);

    private void HandleOptionsChanged(object? sender, GlanceModuleOptionsChangedEventArgs<DevicePresenceSettings> args) =>
        dispatcherQueue.TryEnqueue(() =>
        {
            if (devicePresenceService.IsReady)
            {
                IReadOnlyList<ConnectedBluetoothDevice> devices = devicePresenceService.GetConnectedDevices();
                viewModel.Update(devices, null);
                attentionTracker.EstablishBaseline(devices);
            }
        });

    private void HandleDevicesChanged(object? sender, EventArgs args)
    {
        if (!devicePresenceService.IsReady)
        {
            return;
        }

        IReadOnlyList<ConnectedBluetoothDevice> devices = devicePresenceService.GetConnectedDevices();
        dispatcherQueue.TryEnqueue(() => ApplyDevices(devices));
    }

    private void ApplyDevices(IReadOnlyList<ConnectedBluetoothDevice> devices)
    {
        ConnectedBluetoothDevice? attentionDevice = attentionTracker.Update(devices, LowBatteryThreshold);

        viewModel.Update(devices, attentionDevice?.Id);

        if (attentionDevice is not null)
        {
            attentionService.RequestAttention(Id);
        }
    }
}
