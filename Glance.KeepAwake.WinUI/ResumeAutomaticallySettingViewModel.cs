using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.KeepAwake.WinUI;

public sealed partial class KeepAwakeResumeAutomaticallySettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, KeepAwakeSettings settings, IWritableOptions<KeepAwakeSettings> writer) :
    ModuleSettingViewModel<KeepAwakeSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "KeepAwake", 10, config => config.ResumeAutomatically, (config, value) => config.ResumeAutomatically = value);
