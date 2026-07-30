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

public sealed partial class WeatherTimePreviewSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 40, config => (int)config.PreviewTime, (config, value) => config.PreviewTime = Enum.IsDefined(typeof(WeatherTimeOfDay), value) ? (WeatherTimeOfDay)value : WeatherTimeOfDay.Live);

public sealed partial class WeatherSkyPreviewSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 50, config => (int)config.PreviewSky, (config, value) => config.PreviewSky = Enum.IsDefined(typeof(WeatherSky), value) ? (WeatherSky)value : WeatherSky.Live);

public sealed partial class WeatherEffectPreviewSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 60, config => (int)config.PreviewEffect, (config, value) => config.PreviewEffect = Enum.IsDefined(typeof(WeatherEffect), value) ? (WeatherEffect)value : WeatherEffect.Live);

public sealed partial class WeatherTemperaturePreviewSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, int>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 70, config => (int)config.PreviewTemperature, (config, value) => config.PreviewTemperature = Enum.IsDefined(typeof(WeatherTemperature), value) ? (WeatherTemperature)value : WeatherTemperature.Live);
