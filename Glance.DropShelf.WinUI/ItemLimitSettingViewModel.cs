using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.DropShelf.WinUI;

public sealed partial class ItemLimitSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, DropShelfSettings settings, IWritableOptions<DropShelfSettings> writer) :
    ModuleSettingViewModel<DropShelfSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "DropShelf", 10, config => config.ItemLimit, (config, value) => config.ItemLimit = Math.Clamp(value, 1, 50));
