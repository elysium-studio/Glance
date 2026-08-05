using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;

namespace Glance.ScreenRecorder.WinUI;

public sealed partial class IncludeCursorSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ScreenRecorderSettings settings, IWritableOptions<ScreenRecorderSettings> writer) :
    ModuleSettingViewModel<ScreenRecorderSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ScreenRecorder", 20, config => config.IncludeCursor, (config, value) => config.IncludeCursor = value);
