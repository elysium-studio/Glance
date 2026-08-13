using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ScreenRecorder.WinUI;

public sealed class ScreenRecorderModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<ScreenRecorderSettings>("ScreenRecorder", "screen-recorder.settings.dat", ScreenRecorderJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ScreenRecorderModule>>();
        _ = services.AddSingleton<IScreenRecordingService>(provider =>
            new WindowsScreenRecordingService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Glance Recordings"), provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenRecorderModule>>(), provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WindowsScreenRecordingService>>()));
        _ = services.AddSingleton(provider => new ScreenRecorderViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenRecorderModule>>(), provider.GetRequiredService<GlanceModuleOptions<ScreenRecorderSettings>>().Current));
        _ = services.AddSingleton<ScreenRecorderComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ScreenRecorderComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ScreenRecorderComponent>());
        _ = services.AddViewFor<CountdownSecondsSettingView, IGlanceModuleSettingViewModel, CountdownSecondsSettingViewModel>(ServiceLifetime.Transient, provider => new CountdownSecondsSettingView(), provider => new CountdownSecondsSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ScreenRecorderSettings>>().Current, provider.GetRequiredService<IWritableOptions<ScreenRecorderSettings>>()));
        _ = services.AddViewFor<IncludeCursorSettingView, IGlanceModuleSettingViewModel, IncludeCursorSettingViewModel>(ServiceLifetime.Transient, provider => new IncludeCursorSettingView(), provider => new IncludeCursorSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ScreenRecorderSettings>>().Current, provider.GetRequiredService<IWritableOptions<ScreenRecorderSettings>>()));
        _ = services.AddViewFor<ScreenRecorderRecentRecordingLimitSettingView, IGlanceModuleSettingViewModel, ScreenRecorderRecentRecordingLimitSettingViewModel>(ServiceLifetime.Transient, provider => new ScreenRecorderRecentRecordingLimitSettingView(), provider => new ScreenRecorderRecentRecordingLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ScreenRecorderSettings>>().Current, provider.GetRequiredService<IWritableOptions<ScreenRecorderSettings>>()));
    }
}
