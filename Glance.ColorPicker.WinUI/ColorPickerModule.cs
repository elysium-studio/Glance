using CommunityToolkit.Mvvm.Messaging;
using Elysium.Application.Abstractions;
using Elysium.Application.DependencyInjection;
using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorPickerModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddModuleOptions<ColorPickerSettings>("ColorPicker", "color-picker.settings.dat", ColorPickerJsonContext.Default);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ColorPickerModule>>();
        _ = services.AddSingleton(new ColorHistoryRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "ColorPicker", "colors.db")));
        _ = services.AddSingleton<IColorPickerService, WindowsColorPickerService>();
        _ = services.AddSingleton<ITextCopyService, WindowsTextCopyService>();
        _ = services.AddSingleton(provider => new ColorPickerViewModel(provider.GetRequiredService<IColorPickerService>(), provider.GetRequiredService<ITextCopyService>(), provider.GetRequiredService<GlanceModuleOptions<ColorPickerSettings>>().Current, provider.GetRequiredService<ColorHistoryRepository>()));
        _ = services.AddSingleton<ColorPickerComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ColorPickerComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ColorPickerComponent>());
        _ = services.AddViewFor<RecentColorLimitSettingView, IGlanceModuleSettingViewModel, RecentColorLimitSettingViewModel>(ServiceLifetime.Transient, provider => new RecentColorLimitSettingView(), provider => new RecentColorLimitSettingViewModel(provider, provider.GetRequiredService<IServiceFactory>(), provider.GetRequiredService<IMessenger>(), provider.GetRequiredService<IDisposer>(), provider.GetRequiredService<IDispatcher>(), provider.GetRequiredService<GlanceModuleOptions<ColorPickerSettings>>().Current, provider.GetRequiredService<IWritableOptions<ColorPickerSettings>>()));
    }
}
