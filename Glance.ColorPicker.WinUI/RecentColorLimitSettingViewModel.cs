using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.ColorPicker.WinUI;

public sealed partial class RecentColorLimitSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ColorPickerSettings settings, IWritableOptions<ColorPickerSettings> writer) :
    ModuleSettingViewModel<ColorPickerSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ColorPicker", 10, config => config.RecentColorLimit, (config, value) => config.RecentColorLimit = Math.Clamp(value, 1, 12));
