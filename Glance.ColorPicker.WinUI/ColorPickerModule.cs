using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorPickerModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddModuleOptions<ColorPickerSettings>("ColorPicker", "color-picker.settings.dat", ColorPickerJsonContext.Default);
        services.AddSingleton<ModuleResourceTextLocalizer<ColorPickerModule>>();
        services.AddSingleton<IColorPickerService, WindowsColorPickerService>();
        services.AddSingleton<ITextCopyService, WindowsTextCopyService>();
        services.AddSingleton(provider => new ColorPickerViewModel(provider.GetRequiredService<IColorPickerService>(), provider.GetRequiredService<ITextCopyService>(), provider.GetRequiredService<GlanceModuleOptions<ColorPickerSettings>>().Current));
        services.AddSingleton<IGlanceComponent, ColorPickerComponent>();
        services.AddViewFor<RecentColorLimitSettingView, IGlanceModuleSettingViewModel, RecentColorLimitSettingViewModel>(ServiceLifetime.Transient, provider => new RecentColorLimitSettingView(), provider => new RecentColorLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ColorPickerSettings>>().Current, provider.GetRequiredService<IWritableOptions<ColorPickerSettings>>()));
    }
}
