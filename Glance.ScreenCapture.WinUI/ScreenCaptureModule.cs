using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<ScreenCaptureSettings>("ScreenCapture", "screen-capture.settings.dat", ScreenCaptureJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ScreenCaptureModule>>();
        _ = services.AddSingleton(new ScreenCaptureRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "ScreenCapture", "screen-captures.db")));
        _ = services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
        _ = services.AddSingleton(provider => new ScreenCaptureViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenCaptureModule>>(), provider.GetRequiredService<GlanceModuleOptions<ScreenCaptureSettings>>().Current));
        _ = services.AddSingleton<ScreenCaptureComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ScreenCaptureComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ScreenCaptureComponent>());
        _ = services.AddSingleton<IGlanceAssistantCommandHandler, ScreenCaptureAssistantCommandHandler>();
        _ = services.AddViewFor<RecentCaptureLimitSettingView, IGlanceModuleSettingViewModel, RecentCaptureLimitSettingViewModel>(ServiceLifetime.Transient, provider => new RecentCaptureLimitSettingView(), provider => new RecentCaptureLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ScreenCaptureSettings>>().Current, provider.GetRequiredService<IWritableOptions<ScreenCaptureSettings>>()));
        _ = services.AddViewFor<CopyToClipboardAutomaticallySettingView, IGlanceModuleSettingViewModel, CopyToClipboardAutomaticallySettingViewModel>(ServiceLifetime.Transient, provider => new CopyToClipboardAutomaticallySettingView(), provider => new CopyToClipboardAutomaticallySettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ScreenCaptureSettings>>().Current, provider.GetRequiredService<IWritableOptions<ScreenCaptureSettings>>()));
    }
}
