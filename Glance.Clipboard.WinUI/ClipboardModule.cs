using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Clipboard.WinUI;

public sealed class ClipboardModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<ClipboardSettings>("Clipboard", "clipboard.settings.dat", ClipboardJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<ClipboardModule>>();
        services.AddSingleton(provider => new ClipboardShelfViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ClipboardModule>>()));
        services.AddSingleton<IGlanceComponent, ClipboardComponent>();
        services.AddViewFor<ClipboardHistoryLimitSettingView, IGlanceModuleSettingViewModel, ClipboardHistoryLimitSettingViewModel>(ServiceLifetime.Transient, provider => new ClipboardHistoryLimitSettingView(), provider => new ClipboardHistoryLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ClipboardSettings>>().Current, provider.GetRequiredService<IWritableOptions<ClipboardSettings>>()));
    }
}
