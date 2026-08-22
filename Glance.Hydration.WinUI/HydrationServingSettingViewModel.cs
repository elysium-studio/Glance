using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Hydration.WinUI;

public sealed partial class HydrationServingSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, HydrationSettings settings, IWritableOptions<HydrationSettings> writer) :
    ModuleSettingViewModel<HydrationSettings, double>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Hydration", 20, config => config.ServingSizeMillilitres, (config, value) => config.ServingSizeMillilitres = HydrationSettings.NormalizeServingSize(value));
