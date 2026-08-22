using Elysium.Application.Abstractions;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Glance.Settings;

public static class GlanceSettingsServiceCollectionExtensions
{
    public static GlanceSettingsBuilder<TOptions> AddGlanceSettings<TOptions>(this IServiceCollection services, string schemaId, string sectionPath, string filePath, JsonSerializerOptions jsonOptions)
        where TOptions : class, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        GlanceSettingsRegistration<TOptions> registration = new(schemaId, sectionPath, filePath, jsonOptions);
        _ = services.AddSingleton(registration);
        _ = services.AddSingleton<JsonGlanceSettingsStore<TOptions>>();
        _ = services.AddSingleton<IWritableOptions<TOptions>>(provider => provider.GetRequiredService<JsonGlanceSettingsStore<TOptions>>());
        _ = services.AddTransient(provider => provider.GetRequiredService<IWritableOptions<TOptions>>().ReadAsync().GetAwaiter().GetResult() ?? new TOptions());
        _ = services.AddSingleton<GlanceSettingsOptionsMonitor<TOptions>>();
        _ = services.AddSingleton<IOptionsMonitor<TOptions>>(provider => provider.GetRequiredService<GlanceSettingsOptionsMonitor<TOptions>>());
        _ = services.AddSingleton<IGlanceSettingsChangePublisher<TOptions>>(provider => provider.GetRequiredService<GlanceSettingsOptionsMonitor<TOptions>>());
        _ = services.AddSingleton<IHostedService>(provider => new GlanceSettingsMonitorService<TOptions>(
            provider.GetRequiredService<JsonGlanceSettingsStore<TOptions>>(),
            provider.GetRequiredService<IGlanceSettingsChangePublisher<TOptions>>(),
            provider,
            Path.Combine(provider.GetRequiredService<IHostEnvironment>().ContentRootPath, registration.FilePath),
            provider.GetRequiredService<ILogger<GlanceSettingsMonitorService<TOptions>>>()));

        return new GlanceSettingsBuilder<TOptions>(services, registration);
    }
}
