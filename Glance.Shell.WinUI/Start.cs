using Elysium.Platform.Windows;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;
using Velopack;

namespace Glance.Shell.WinUI;

public static class Start
{
    private const uint MessageBoxIconError = 0x00000010;
    private const string RestartAfterArgument = "--restart-after";

    [STAThread]
    public static void Main(string[] args)
    {
        WaitForRestartSource(args);

        if (!PackageIdentity.IsFullPackage)
        {
            VelopackApp.Build()
                .OnAfterInstallFastCallback(ExternalPackageIdentity.Register)
                .OnAfterUpdateFastCallback(ExternalPackageIdentity.Register)
                .OnBeforeUninstallFastCallback(UninstallCleanup.Run)
                .Run();
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            _ = MessageBoxW(0,
                "Glance requires Windows 11, build 22000 or later. Windows 10 is not supported.",
                "Glance requires Windows 11",
                MessageBoxIconError);
            return;
        }

        AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance instance = AppInstance.FindOrRegisterForKey($"{Environment.UserName}.Glance");

        if (!instance.IsCurrent)
        {
            instance.RedirectActivationToAsync(activation).GetAwaiter().GetResult();
            return;
        }

        using SingleInstanceGuard? instanceGuard = SingleInstanceGuard.TryAcquire($"{Environment.UserName}.Glance");

        if (instanceGuard is null)
        {
            return;
        }

#pragma warning disable CA1806
        Microsoft.UI.Xaml.Application.Start(_ => new App(instance, activation));
#pragma warning restore CA1806
    }

    private static void WaitForRestartSource(string[] args)
    {
        int argumentIndex = Array.FindIndex(args,
            argument => string.Equals(argument, RestartAfterArgument, StringComparison.OrdinalIgnoreCase));

        if (argumentIndex < 0 ||
            argumentIndex + 1 >= args.Length ||
            !int.TryParse(args[argumentIndex + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int processId) ||
            processId == Environment.ProcessId)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            _ = process.WaitForExit(60_000);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(nint window,
        string text,
        string caption,
        uint type);
}
