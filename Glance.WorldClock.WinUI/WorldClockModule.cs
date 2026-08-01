using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Glance.WorldClock.WinUI;

public sealed class WorldClockModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<WorldClockSettings>("WorldClock", "world-clock.settings.dat", WorldClockJsonContext.Default);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ModuleResourceTextLocalizer<WorldClockModule>>();
        services.AddSingleton(CreateViewModel);
        services.AddSingleton<IGlanceComponent, WorldClockComponent>();
        services.AddViewFor<WorldClockTimeFormatSettingView, IGlanceModuleSettingViewModel, WorldClockTimeFormatSettingViewModel>(ServiceLifetime.Transient, provider => new WorldClockTimeFormatSettingView(), provider => new WorldClockTimeFormatSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<WorldClockSettings>>().Current, provider.GetRequiredService<IWritableOptions<WorldClockSettings>>()));
    }

    private static WorldClockViewModel CreateViewModel(IServiceProvider provider)
    {
        ModuleResourceTextLocalizer<WorldClockModule> localizer = provider.GetRequiredService<ModuleResourceTextLocalizer<WorldClockModule>>();
        WorldClockViewModel viewModel = new(CreateClocks(localizer));
        viewModel.Initialize();
        return viewModel;
    }

    private static IEnumerable<WorldClockDefinition> CreateClocks(ITextLocalizer localizer)
    {
        yield return new WorldClockDefinition("Local", localizer.GetText("LocalClock"), TimeZoneInfo.Local);

        foreach ((string id, string resourceKey) in new[]
        {
            ("GMT Standard Time", "LondonClock"),
            ("Eastern Standard Time", "NewYorkClock"),
            ("Tokyo Standard Time", "TokyoClock"),
            ("AUS Eastern Standard Time", "SydneyClock")
        })
        {
            TimeZoneInfo? timeZone = FindTimeZone(id);

            if (timeZone is not null && !string.Equals(timeZone.Id, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase))
            {
                yield return new WorldClockDefinition(id, localizer.GetText(resourceKey), timeZone);
            }
        }
    }

    private static TimeZoneInfo? FindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
