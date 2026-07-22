using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Clipboard.WinUI;

public sealed partial class ClipboardHistoryLimitSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ClipboardSettings settings, IWritableOptions<ClipboardSettings> writer) :
    ModuleSettingViewModel<ClipboardSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Clipboard", 10, config => config.HistoryLimit, (config, value) => config.HistoryLimit = Math.Clamp(value, 1, 20));
