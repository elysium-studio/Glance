using CommunityToolkit.Mvvm.ComponentModel;
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

public sealed partial class WeatherDebugPreviewSettingViewModel(IServiceProvider provider, IServiceFactory factory, IMessenger messenger, IDisposer disposer, IDispatcher dispatcher, WeatherSettings settings, IWritableOptions<WeatherSettings> writer) :
    ModuleSettingViewModel<WeatherSettings, bool>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 40, config => config.DebugPreviewEnabled, UpdatePreview)
{
    private bool receivingPreview;

    [ObservableProperty]
    public partial int PreviewCelestialIndex { get; set; }

    [ObservableProperty]
    public partial int PreviewEffectIndex { get; set; }

    [ObservableProperty]
    public partial int PreviewSkyIndex { get; set; }

    [ObservableProperty]
    public partial int PreviewTemperatureIndex { get; set; }

    [ObservableProperty]
    public partial int PreviewTimeIndex { get; set; }

    public override void Activated()
    {
        base.Activated();
        ReadPreview(Options);
    }

    protected override void OptionsChanged(WeatherSettings options) => ReadPreview(options);

    partial void OnPreviewCelestialIndexChanged(int value) => WritePreview(config => config.PreviewCelestial = (WeatherCelestial)(value + 1));

    partial void OnPreviewEffectIndexChanged(int value) => WritePreview(config => config.PreviewEffect = (WeatherEffect)(value + 1));

    partial void OnPreviewSkyIndexChanged(int value) => WritePreview(config => config.PreviewSky = (WeatherSky)(value + 1));

    partial void OnPreviewTemperatureIndexChanged(int value) => WritePreview(config => config.PreviewTemperature = (WeatherTemperature)(value + 1));

    partial void OnPreviewTimeIndexChanged(int value) => WritePreview(config => config.PreviewTime = (WeatherTimeOfDay)(value + 1));

    private void ReadPreview(WeatherSettings settings)
    {
        receivingPreview = true;
        PreviewTimeIndex = Math.Max(0, (int)settings.PreviewTime - 1);
        PreviewSkyIndex = Math.Max(0, (int)settings.PreviewSky - 1);
        PreviewCelestialIndex = Math.Max(0, (int)settings.PreviewCelestial - 1);
        PreviewEffectIndex = Math.Max(0, (int)settings.PreviewEffect - 1);
        PreviewTemperatureIndex = Math.Max(0, (int)settings.PreviewTemperature - 1);
        receivingPreview = false;
    }

    private void WritePreview(Action<WeatherSettings> update)
    {
        if (IsActive && !receivingPreview)
        {
            _ = Writer.WriteAsync(update);
        }
    }

    private static void UpdatePreview(WeatherSettings settings, bool enabled)
    {
        settings.DebugPreviewEnabled = enabled;

        if (!enabled)
        {
            return;
        }

        settings.PreviewTime = settings.PreviewTime == WeatherTimeOfDay.Live ? WeatherTimeOfDay.Afternoon : settings.PreviewTime;
        settings.PreviewSky = settings.PreviewSky == WeatherSky.Live ? WeatherSky.PartlyCloudy : settings.PreviewSky;
        settings.PreviewCelestial = settings.PreviewCelestial == WeatherCelestial.Live ? WeatherCelestial.Sun : settings.PreviewCelestial;
        settings.PreviewEffect = settings.PreviewEffect == WeatherEffect.Live ? WeatherEffect.None : settings.PreviewEffect;
        settings.PreviewTemperature = settings.PreviewTemperature == WeatherTemperature.Live ? WeatherTemperature.Normal : settings.PreviewTemperature;
    }
}
