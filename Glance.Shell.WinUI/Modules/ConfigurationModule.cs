using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Elysium.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Glance.Shell.WinUI;

public sealed class ConfigurationModule :
    IModule
{
    public void Register(IServiceCollection services)
    {
        WritableOptionsBuilder<GlanceSettings> builder =
            new(services, "Settings", "settings.dat");

        builder
            .WithJsonOptions(new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                TypeInfoResolverChain = { GlanceJsonContext.Default }
            })
            .UseJson().WithChangeHandler((provider, options, name) =>
                provider.GetRequiredService<IMessenger>().Send(new OptionsChangedEventArgs<GlanceSettings>(options)))
            .WithAsyncChangeHandler(async (provider, options, _) =>
            {
                IStartupManager startupManager =
                    provider.GetRequiredService<IStartupManager>();

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
