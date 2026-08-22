using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationGoalSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, HydrationSettings settings, IWritableOptions<HydrationSettings> writer) :
    ModuleSettingViewModel<HydrationSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Hydration", 10, config => config.DailyGoalMillilitres, (config, value) => config.DailyGoalMillilitres = HydrationSettings.NormalizeDailyGoal(value));
