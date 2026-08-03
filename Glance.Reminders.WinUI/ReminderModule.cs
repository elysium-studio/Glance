using Glance.Application.Abstractions;
using Glance.UI.WinUI;
using Microsoft.Extensions.DependencyInjection;

namespace Glance.Reminders.WinUI;

public sealed class ReminderModule :
    IGlanceModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ModuleResourceTextLocalizer<ReminderModule>>();
        services.AddSingleton(new ReminderRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glance", "Reminders", "reminders.db")));
        services.AddSingleton(provider => new ReminderViewModel(provider.GetRequiredService<ModuleResourceTextLocalizer<ReminderModule>>()));
        services.AddSingleton<ReminderComponent>();
        services.AddSingleton<IGlanceComponent>(provider => provider.GetRequiredService<ReminderComponent>());
        services.AddSingleton<IGlanceActionProvider>(provider => provider.GetRequiredService<ReminderComponent>());
    }
}
