using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.SystemMonitor.WinUI;

public sealed partial class PerformanceChartsSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, SystemMonitorSettings settings, IWritableOptions<SystemMonitorSettings> writer) :
    ModuleSettingViewModel<SystemMonitorSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "SystemMonitor", 10, config => config.ShowPerformanceCharts, (config, value) => config.ShowPerformanceCharts = value);
