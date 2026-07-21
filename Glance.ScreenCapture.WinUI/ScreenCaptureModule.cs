using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ScreenCapture.WinUI;

public sealed class ScreenCaptureModule : IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<ScreenCaptureModule>>();
        services.AddSingleton<IScreenCaptureService, WindowsScreenCaptureService>();
        services.AddSingleton(provider => new ScreenCaptureViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ScreenCaptureModule>>()));
        services.AddSingleton<IGlanceComponent, ScreenCaptureComponent>();
    }
}
