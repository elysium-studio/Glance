using Elysium.Application;
using Glance.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Settings;

public sealed class GlanceSettingsBuilder<TOptions>
    where TOptions : class, new()
{
    private readonly GlanceSettingsRegistration<TOptions> registration;
    private readonly IServiceCollection services;

    internal GlanceSettingsBuilder(IServiceCollection services, GlanceSettingsRegistration<TOptions> registration)
    {
        this.services = services;
        this.registration = registration;
    }

    public GlanceSettingsBuilder<TOptions> AddMigration<TMigration>()
        where TMigration : class, IGlanceSettingsMigration<TOptions>
    {
        _ = services.AddSingleton<IGlanceSettingsMigration<TOptions>, TMigration>();
        return this;
    }

    public GlanceSettingsBuilder<TOptions> WithAsyncChangeHandler(Func<IServiceProvider, TOptions, string?, Task> handler)
    {
        _ = services.AddTransient<IAsyncOptionsChangeHandler<TOptions>>(provider => new DelegateAsyncOptionsChangeHandler<TOptions>(provider, handler));
        return this;
    }

    public GlanceSettingsBuilder<TOptions> WithChangeHandler(Action<IServiceProvider, TOptions, string?> handler)
    {
        _ = services.AddTransient<IOptionsChangeHandler<TOptions>>(provider => new DelegateOptionsChangeHandler<TOptions>(provider, handler));
        return this;
    }

    public GlanceSettingsBuilder<TOptions> WithChangeHandler<THandler>()
        where THandler : class, IOptionsChangeHandler<TOptions>
    {
        _ = services.AddTransient<IOptionsChangeHandler<TOptions>, THandler>();
        return this;
    }

    public GlanceSettingsBuilder<TOptions> WithSchemaVersion(int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        registration.Version = version;
        return this;
    }
}
