using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<ScreenCaptureSettings>("ScreenCapture", "screen-capture.settings.dat", ScreenCaptureJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<ScreenCaptureModule>>();
        services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
        services.AddSingleton(provider => new ScreenCaptureViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenCaptureModule>>(), provider.GetRequiredService<GlanceModuleOptions<ScreenCaptureSettings>>().Current));
        services.AddSingleton<IGlanceComponent, ScreenCaptureComponent>();
        services.AddViewFor<RecentCaptureLimitSettingView, IGlanceModuleSettingViewModel, RecentCaptureLimitSettingViewModel>(ServiceLifetime.Transient, provider => new RecentCaptureLimitSettingView(), provider => new RecentCaptureLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ScreenCaptureSettings>>().Current, provider.GetRequiredService<IWritableOptions<ScreenCaptureSettings>>()));
    }
}
