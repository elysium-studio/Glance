using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Glance.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glance.UI.WinUI;

public static class ModuleSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddModuleOptions<TOptions>(this IServiceCollection services, string sectionPath, string filePath, JsonSerializerContext context)
        where TOptions : class, new()
        => services.AddModuleOptions<TOptions>(sectionPath, filePath, context, _ => { });

    public static IServiceCollection AddModuleOptions<TOptions>(this IServiceCollection services, string sectionPath, string filePath, JsonSerializerContext context, Action<GlanceSettingsBuilder<TOptions>> configure)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(configure);
        JsonSerializerOptions jsonOptions = GetJsonOptions(services);

        if (!jsonOptions.TypeInfoResolverChain.Contains(context))
        {
            jsonOptions.TypeInfoResolverChain.Add(context);
        }

        GlanceSettingsBuilder<TOptions> builder = services.AddGlanceSettings<TOptions>(
            $"glance.module.settings/{sectionPath}",
            sectionPath,
            Path.Combine("Modules", "Data", sectionPath, filePath),
            jsonOptions);
        _ = builder.WithChangeHandler((provider, options, _) =>
        {
            provider.GetRequiredService<GlanceModuleOptions<TOptions>>().Update(options);
            provider.GetRequiredService<IMessenger>().Send(new OptionsChangedEventArgs<TOptions>(options));
        });

        _ = services.AddSingleton(provider => new GlanceModuleOptions<TOptions>(provider.GetRequiredService<TOptions>()));
        configure(builder);
        return services;
    }

    private static JsonSerializerOptions GetJsonOptions(IServiceCollection services)
    {
        JsonSerializerOptions? options = services
            .LastOrDefault(descriptor => descriptor.ServiceType == typeof(JsonSerializerOptions))?
            .ImplementationInstance as JsonSerializerOptions;

        if (options is not null)
        {
            return options;
        }

        options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        _ = services.AddSingleton(options);
        return options;
    }
}
