using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Glance.Clipboard.WinUI;

public sealed class ClipboardModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<ClipboardSettings>("Clipboard", "clipboard.settings.dat", ClipboardJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ClipboardModule>>();
        _ = services.AddSingleton(new ClipboardRepository(GlanceModuleData.GetPath("Clipboard", "clipboard.db")));
        _ = services.AddSingleton(provider => new ClipboardShelfViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ClipboardModule>>()));
        _ = services.AddSingleton<IGlanceComponent, ClipboardComponent>();
        _ = services.AddViewFor<ClipboardHistoryLimitSettingView, IGlanceModuleSettingViewModel, ClipboardHistoryLimitSettingViewModel>(ServiceLifetime.Transient, provider => new ClipboardHistoryLimitSettingView(), provider => new ClipboardHistoryLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ClipboardSettings>>().Current, provider.GetRequiredService<IWritableOptions<ClipboardSettings>>()));
    }
}
