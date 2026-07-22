using Elysium.Platform.Windows;
using System;
using Velopack;

namespace Glance.Shell.WinUI;

public static class Start
{
    [STAThread]
    public static void Main()
    {
        using SingleInstanceGuard? instanceGuard = SingleInstanceGuard.TryAcquire($"{Environment.UserName}.Glance");

        if (instanceGuard is null)
        {
            return;
        }

        if (!PackageIdentity.IsPackaged)
        {
            VelopackApp.Build()
                .OnBeforeUninstallFastCallback(UninstallCleanup.Run)
                .Run();
        }

#pragma warning disable CA1806
        Microsoft.UI.Xaml.Application.Start(_ => new App());
#pragma warning restore CA1806
    }
}
