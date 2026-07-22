using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Glance.DevicePresence.WinUI;

public sealed class DevicePresenceComponent :
    IGlanceComponent,
    IGlanceConnectedAnimationComponent,
    IDisposable
{
    private readonly IGlanceAttentionService attentionService;
    private readonly IDevicePresenceService devicePresenceService;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly ModuleResourceTextLocalizer<DevicePresenceModule> localizer;
    private readonly DevicePresenceViewModel viewModel;
    private readonly GlanceModuleOptions<DevicePresenceSettings> options;
    private HashSet<string> currentDeviceIds = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> lowBatteryDeviceIds = new(StringComparer.OrdinalIgnoreCase);
    private bool hasSnapshot;

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
                currentDeviceIds = devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                lowBatteryDeviceIds = devices.Where(device => device.BatteryLevel <= LowBatteryThreshold).Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                hasSnapshot = true;
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
        ConnectedBluetoothDevice? addedDevice = hasSnapshot ? devices.FirstOrDefault(device => !currentDeviceIds.Contains(device.Id)) : null;
        ConnectedBluetoothDevice? newlyLowDevice = hasSnapshot ? devices.FirstOrDefault(device => device.BatteryLevel <= LowBatteryThreshold && !lowBatteryDeviceIds.Contains(device.Id)) : null;
        ConnectedBluetoothDevice? attentionDevice = addedDevice ?? newlyLowDevice;

        viewModel.Update(devices, attentionDevice?.Id);
        currentDeviceIds = devices.Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lowBatteryDeviceIds = devices.Where(device => device.BatteryLevel <= LowBatteryThreshold).Select(device => device.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (attentionDevice is not null)
        {
            attentionService.RequestAttention(Id);
        }

        hasSnapshot = true;
    }
}
