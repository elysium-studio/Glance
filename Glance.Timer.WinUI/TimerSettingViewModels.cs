using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Timer.WinUI;

public sealed partial class TimerDefaultDurationSettingViewModel(
    IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    TimerSettings settings,
    IWritableOptions<TimerSettings> writer) :
    ModuleSettingViewModel<TimerSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Timer", 10, config => config.DefaultDurationMinutes, (config, value) => config.DefaultDurationMinutes = Math.Clamp(value, 1, 1440));

public sealed partial class TimerAdjustmentSettingViewModel(
    IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    TimerSettings settings,
    IWritableOptions<TimerSettings> writer) :
    ModuleSettingViewModel<TimerSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Timer", 20, config => config.AdjustmentMinutes, (config, value) => config.AdjustmentMinutes = Math.Clamp(value, 0.5, 60));
