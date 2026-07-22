using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Power.WinUI;

public sealed partial class LowBatteryThresholdSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, PowerSettings settings, IWritableOptions<PowerSettings> writer) :
    ModuleSettingViewModel<PowerSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Power", 10, config => config.LowBatteryThreshold, (config, value) => config.LowBatteryThreshold = Math.Clamp(value, 10, 50));

public sealed partial class CriticalBatteryThresholdSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, PowerSettings settings, IWritableOptions<PowerSettings> writer) :
    ModuleSettingViewModel<PowerSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Power", 20, config => config.CriticalBatteryThreshold, (config, value) => config.CriticalBatteryThreshold = Math.Clamp(value, 5, 20));
