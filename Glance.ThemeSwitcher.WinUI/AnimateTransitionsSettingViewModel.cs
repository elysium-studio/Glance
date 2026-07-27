using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.ThemeSwitcher.WinUI;

public sealed partial class AnimateTransitionsSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, ThemeSwitcherSettings settings, IWritableOptions<ThemeSwitcherSettings> writer) :
    ModuleSettingViewModel<ThemeSwitcherSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "ThemeSwitcher", 10, config => config.AnimateTransitions, (config, value) => config.AnimateTransitions = value);
