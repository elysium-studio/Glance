using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class ScreenRecorderRecentRecordingLimitSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ScreenRecorderSettings settings, IWritableOptions<ScreenRecorderSettings> writer) :
    ModuleSettingViewModel<ScreenRecorderSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ScreenRecorder", 30, config => config.RecentRecordingLimit, (config, value) => config.RecentRecordingLimit = Math.Clamp(value, 1, 12));
