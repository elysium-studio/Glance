using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.VoiceNotes.WinUI;

public sealed partial class RecentRecordingLimitSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, VoiceNotesSettings settings, IWritableOptions<VoiceNotesSettings> writer) :
    ModuleSettingViewModel<VoiceNotesSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "VoiceNotes", 10, config => config.RecentRecordingLimit, (config, value) => config.RecentRecordingLimit = Math.Clamp(value, 1, 10));
