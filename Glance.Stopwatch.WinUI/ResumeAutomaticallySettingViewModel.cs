using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Stopwatch.WinUI;

public sealed partial class StopwatchResumeAutomaticallySettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, StopwatchSettings settings, IWritableOptions<StopwatchSettings> writer) :
    ModuleSettingViewModel<StopwatchSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Stopwatch", 10, config => config.ResumeAutomatically, (config, value) => config.ResumeAutomatically = value);
