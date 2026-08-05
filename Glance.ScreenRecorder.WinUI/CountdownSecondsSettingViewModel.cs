using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class CountdownSecondsSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ScreenRecorderSettings settings, IWritableOptions<ScreenRecorderSettings> writer) :
    ModuleSettingViewModel<ScreenRecorderSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ScreenRecorder", 10, config => config.CountdownSeconds, (config, value) => config.CountdownSeconds = Math.Clamp(value, 0, 10));
