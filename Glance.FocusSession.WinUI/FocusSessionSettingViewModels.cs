using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.FocusSession.WinUI;

public sealed partial class FocusDurationSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, FocusSessionSettings settings, IWritableOptions<FocusSessionSettings> writer) :
    ModuleSettingViewModel<FocusSessionSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "FocusSession", 10, config => config.FocusDurationMinutes, (config, value) => config.FocusDurationMinutes = Math.Clamp(value, 1, 180));

public sealed partial class BreakDurationSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, FocusSessionSettings settings, IWritableOptions<FocusSessionSettings> writer) :
    ModuleSettingViewModel<FocusSessionSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "FocusSession", 20, config => config.BreakDurationMinutes, (config, value) => config.BreakDurationMinutes = Math.Clamp(value, 1, 60));
