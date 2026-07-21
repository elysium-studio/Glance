using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.ColorPicker.WinUI;

public sealed class ColorPickerModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<ModuleResourceTextLocalizer<ColorPickerModule>>();
        services.AddSingleton<IColorPickerService, WindowsColorPickerService>();
        services.AddSingleton<ITextCopyService, WindowsTextCopyService>();
        services.AddSingleton<ColorPickerViewModel>();
        services.AddSingleton<IGlanceComponent, ColorPickerComponent>();
    }
}
