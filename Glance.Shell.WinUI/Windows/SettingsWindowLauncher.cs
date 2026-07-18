using Glance.Shell;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Glance.Shell.WinUI;

public sealed class SettingsWindowLauncher(IServiceProvider provider) : ISettingsLauncher
{
    private SettingsWindow? window;

    public void Show()
    {
        if (window is null)
        {
            window = provider.GetRequiredKeyedService<SettingsWindow>("SettingsWindow");
            window.Closed += (_, _) => window = null;
        }

        window.Activate();
    }
}
