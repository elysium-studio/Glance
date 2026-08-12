using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Torrents.WinUI;

public sealed class TorrentModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<TorrentSettings>("Torrent", "torrent.settings.dat", TorrentJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<TorrentModule>>();
        _ = services.AddSingleton<TorrentsViewModel>();
        _ = services.AddSingleton<MonoTorrentEngineService>();
        _ = services.AddSingleton<ITorrentEngineService>(provider => provider.GetRequiredService<MonoTorrentEngineService>());
        _ = services.AddSingleton<TorrentAddCoordinator>();
        _ = services.AddViewFor<TorrentSettingsView, IGlanceModuleSettingViewModel, TorrentSettingsViewModel>(ServiceLifetime.Transient,
            _ => new TorrentSettingsView(),
            provider => new TorrentSettingsViewModel(provider.GetRequiredService<GlanceModuleOptions<TorrentSettings>>().Current, provider.GetRequiredService<IWritableOptions<TorrentSettings>>()));
        _ = services.AddSingleton<IGlanceComponent, TorrentComponent>();
        _ = services.AddSingleton<IGlanceIntent>(provider => new TorrentIntentAdapter(provider.GetServices<IGlanceComponent>().OfType<TorrentComponent>().Single()));
    }

    private sealed class TorrentIntentAdapter(TorrentComponent component) : IGlanceIntent
    {
        public GlanceIntentDescriptor Descriptor => component.Descriptor;
        public bool CanHandle(GlanceContentKind kind) => component.CanHandle(kind);
        public bool CanHandle(GlanceContentContext context) => component.CanHandle(context);
        public Task InvokeAsync(GlanceContentContext context, CancellationToken cancellationToken = default) => ((IGlanceIntent)component).InvokeAsync(context, cancellationToken);
    }
}
