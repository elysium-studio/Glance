using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Glance.Settings;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Glance.Shell.WinUI;

public sealed class ConfigurationModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        GlanceSettingsBuilder<GlanceSettings> builder = services.AddGlanceSettings<GlanceSettings>(
            "glance.application.settings",
            "Settings",
            "settings.dat",
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                TypeInfoResolverChain = { GlanceJsonContext.Default }
            });

        _ = builder
            .WithChangeHandler((provider, options, name) =>
                provider.GetRequiredService<IMessenger>().Send(new OptionsChangedEventArgs<GlanceSettings>(options)))
            .WithAsyncChangeHandler(async (provider, options, _) =>
            {
                IStartupManager startupManager = provider.GetRequiredService<IStartupManager>();

                if (options.StartWithWindows)
                {
                    await startupManager.EnableAsync();
                }
                else
                {
                    await startupManager.DisableAsync();
                }
            });
    }
}
