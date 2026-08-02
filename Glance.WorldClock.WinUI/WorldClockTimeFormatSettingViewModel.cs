using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.WorldClock.WinUI;

public sealed partial class WorldClockTimeFormatSettingViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    WorldClockSettings settings,
    IWritableOptions<WorldClockSettings> writer) :
    ModuleSettingViewModel<WorldClockSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, "WorldClock", 10, config => config.Use24HourTime ? 0 : 1, (config, value) => config.Use24HourTime = value == 0);
