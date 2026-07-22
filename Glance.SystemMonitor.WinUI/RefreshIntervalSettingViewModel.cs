using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class RefreshIntervalSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, SystemMonitorSettings settings, IWritableOptions<SystemMonitorSettings> writer) :
    ModuleSettingViewModel<SystemMonitorSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "SystemMonitor", 10, config => config.RefreshIntervalSeconds, (config, value) => config.RefreshIntervalSeconds = Math.Clamp(value, 0.5, 10));
