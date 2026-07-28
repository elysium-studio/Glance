using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Presence.WinUI;

public sealed partial class PresenceResumeAutomaticallySettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, PresenceSettings settings, IWritableOptions<PresenceSettings> writer) :
    ModuleSettingViewModel<PresenceSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Presence", 10, config => config.ResumeAutomatically, (config, value) => config.ResumeAutomatically = value);
