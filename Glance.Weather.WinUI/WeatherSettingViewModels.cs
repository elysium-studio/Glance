using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherApiKeySettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, string>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 10, config => config.ApiKey, (config, value) => config.ApiKey = value?.Trim() ?? string.Empty);

public sealed partial class WeatherLocationSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, string>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 20, config => config.Location, (config, value) => config.Location = value?.Trim() ?? string.Empty);

public sealed partial class WeatherUnitsSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 30, config => config.UseFahrenheit ? 1 : 0, (config, value) => config.UseFahrenheit = value == 1);
