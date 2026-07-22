using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.DevicePresence.WinUI;

public sealed partial class DevicePresenceLowBatteryThresholdSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, DevicePresenceSettings settings, IWritableOptions<DevicePresenceSettings> writer) :
    ModuleSettingViewModel<DevicePresenceSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "DevicePresence", 10, config => config.LowBatteryThreshold, (config, value) => config.LowBatteryThreshold = Math.Clamp(value, 5, 50));
