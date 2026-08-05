using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Reminders.WinUI;

public sealed class ReminderModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton<ModuleResourceTextLocalizer<ReminderModule>>();
        _ = services.AddSingleton(new ReminderRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Reminders", "reminders.db")));
        _ = services.AddSingleton(provider => new ReminderViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ReminderModule>>()));
        _ = services.AddSingleton<ReminderComponent>();
        _ = services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ReminderComponent>());
        _ = services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ReminderComponent>());
    }
}
