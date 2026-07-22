using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glance.UI.WinUI;

public static class ModuleSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddModuleOptions<TOptions>(
        this IServiceCollection services,
        string sectionPath,
        string filePath,
        JsonSerializerContext context)
        where TOptions : class, new()
    {
        JsonSerializerOptions jsonOptions = GetJsonOptions(services);

        if (!jsonOptions.TypeInfoResolverChain.Contains(context))
        {
            jsonOptions.TypeInfoResolverChain.Add(context);
        }

        WritableOptionsBuilder<TOptions> builder = new(services, sectionPath, filePath);
        builder.UseJson().WithChangeHandler((provider, options, _) =>
        {
            provider.GetRequiredService<GlanceModuleOptions<TOptions>>().Update(options);
            provider.GetRequiredService<IMessenger>().Send(new OptionsChangedEventArgs<TOptions>(options));
        });

        services.AddSingleton(provider => new GlanceModuleOptions<TOptions>(provider.GetRequiredService<TOptions>()));
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
        services.AddSingleton(options);
        return options;
    }
}
