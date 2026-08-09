using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.UI.WinUI;
using System;

namespace Glance.Weather.WinUI;

public sealed partial class WeatherApiKeySettingViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    WeatherSettings settings,
    IWritableOptions<WeatherSettings> writer,
    WeatherConfigurationValidator validator,
    ModuleResourceTextLocalizer<WeatherModule> localizer) :
    ModuleSettingViewModel<WeatherSettings, string>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 10, config => config.ApiKey, (config, value) => config.ApiKey = value?.Trim() ?? string.Empty)
{
    [ObservableProperty]
    public partial bool HasValidationError { get; private set; }

    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = string.Empty;

    public override void Activated()
    {
        base.Activated();
        validator.Changed += HandleValidationChanged;
        validator.Update(Options);
        ApplyValidation();
    }

    public override void Deactivated()
    {
        validator.Changed -= HandleValidationChanged;
        base.Deactivated();
    }

    protected override void OptionsChanged(WeatherSettings options) => validator.Update(options);

    protected override void ValueChanged(string? value)
    {
        base.ValueChanged(value);
        validator.UpdateApiKey(value ?? string.Empty);
    }

    private void HandleValidationChanged(object? sender,
        EventArgs args) => Dispatcher.Dispatch(ApplyValidation);

    private void ApplyValidation()
    {
        WeatherConfigurationError error = validator.Current.ApiKeyError;
        HasValidationError = error != WeatherConfigurationError.None;
        ValidationMessage = error switch
        {
            WeatherConfigurationError.Required => localizer.GetText("WeatherApiKeyRequired"),
            WeatherConfigurationError.Invalid => localizer.GetText("WeatherApiKeyInvalidFormat"),
            WeatherConfigurationError.Rejected => localizer.GetText("WeatherApiKeyRejected"),
            WeatherConfigurationError.RateLimited => localizer.GetText("WeatherApiKeyRateLimited"),
            _ => string.Empty
        };
    }
}

public sealed partial class WeatherLocationSettingViewModel(IServiceProvider provider,
    IServiceFactory factory,
    IMessenger messenger,
    IDisposer disposer,
    IDispatcher dispatcher,
    WeatherSettings settings,
    IWritableOptions<WeatherSettings> writer,
    WeatherConfigurationValidator validator,
    ModuleResourceTextLocalizer<WeatherModule> localizer) :
    ModuleSettingViewModel<WeatherSettings, string>(provider, factory, messenger, disposer, dispatcher, settings, writer, "Weather", 20, config => config.Location, (config, value) => config.Location = value?.Trim() ?? string.Empty)
{
    [ObservableProperty]
    public partial bool HasValidationError { get; private set; }

    [ObservableProperty]
    public partial string ValidationMessage { get; private set; } = string.Empty;

    public override void Activated()
    {
        base.Activated();
        validator.Changed += HandleValidationChanged;
        validator.Update(Options);
        ApplyValidation();
    }

    public override void Deactivated()
    {
        validator.Changed -= HandleValidationChanged;
        base.Deactivated();
    }

    protected override void OptionsChanged(WeatherSettings options) => validator.Update(options);

    protected override void ValueChanged(string? value)
    {
        base.ValueChanged(value);
        validator.UpdateLocation(value ?? string.Empty);
    }

    private void HandleValidationChanged(object? sender,
        EventArgs args) => Dispatcher.Dispatch(ApplyValidation);

    private void ApplyValidation()
    {
        WeatherConfigurationError error = validator.Current.LocationError;
        HasValidationError = error != WeatherConfigurationError.None;
        ValidationMessage = error switch
        {
            WeatherConfigurationError.Required => localizer.GetText("WeatherLocationRequired"),
            WeatherConfigurationError.Invalid => localizer.GetText("WeatherLocationInvalid"),
            WeatherConfigurationError.NotFound => localizer.GetText("WeatherLocationNotFound"),
            _ => string.Empty
        };
    }
}

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
    public partial double PreviewHour { get; set; }

    public string PreviewTimeText => TimeSpan.FromHours(PreviewHour % 24).ToString(@"hh\:mm");

    public override void Activated()
    {
        base.Activated();
        ReadPreview(Options);
    }

    protected override void OptionsChanged(WeatherSettings options) => ReadPreview(options);

    partial void OnPreviewCelestialIndexChanged(int value) => WritePreview(config => config.PreviewCelestial = (WeatherCelestial)value);

    partial void OnPreviewEffectIndexChanged(int value) => WritePreview(config => config.PreviewEffect = (WeatherEffect)(value + 1));

    partial void OnPreviewSkyIndexChanged(int value) => WritePreview(config => config.PreviewSky = (WeatherSky)(value + 1));

    partial void OnPreviewTemperatureIndexChanged(int value) => WritePreview(config => config.PreviewTemperature = (WeatherTemperature)(value + 1));

    partial void OnPreviewHourChanged(double value)
    {
        OnPropertyChanged(nameof(PreviewTimeText));
        WritePreview(config => config.PreviewHour = value);
    }

    private void ReadPreview(WeatherSettings settings)
    {
        receivingPreview = true;
        PreviewHour = Math.Clamp(settings.PreviewHour, 0, 24);
        PreviewSkyIndex = Math.Max(0, (int)settings.PreviewSky - 1);
        PreviewCelestialIndex = Math.Max(0, (int)settings.PreviewCelestial);
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

        settings.PreviewSky = settings.PreviewSky == WeatherSky.Live ? WeatherSky.PartlyCloudy : settings.PreviewSky;
        settings.PreviewEffect = settings.PreviewEffect == WeatherEffect.Live ? WeatherEffect.None : settings.PreviewEffect;
        settings.PreviewTemperature = settings.PreviewTemperature == WeatherTemperature.Live ? WeatherTemperature.Normal : settings.PreviewTemperature;
    }
}
