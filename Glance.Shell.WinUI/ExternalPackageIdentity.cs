using System;
using System.Diagnostics;
using System.IO;
using Velopack;
using Windows.ApplicationModel;

namespace Glance.Shell.WinUI;

internal static class ExternalPackageIdentity
{
    private const string IdentityPackageFileName = "Glance.Identity.msix";
    private const string PackageName = "ElysiumStudio.Glance";

    public static void Register(SemanticVersion version)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        string packagePath = Path.Combine(AppContext.BaseDirectory, IdentityPackageFileName);

        if (!File.Exists(packagePath))
        {
            Unregister();
            return;
        }

        if (HasMatchingRegistration(version))
        {
            return;
        }

        RunPowerShell($"Add-AppxPackage -ErrorAction Stop -Path '{EscapePowerShellValue(packagePath)}' -ExternalLocation '{EscapePowerShellValue(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))}'");
    }

    public static void Unregister()
    {
        try
        {
            RunPowerShell($"Get-AppxPackage -Name '{PackageName}' | Remove-AppxPackage -ErrorAction Stop");
        }
        catch
        {
        }
    }

    private static bool HasMatchingRegistration(SemanticVersion version)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return false;
        }

        try
        {
            Package package = Package.Current;
            return string.Equals(package.Id.Name, PackageName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.TrimEndingDirectorySeparator(package.EffectiveExternalPath), Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase) &&
                package.Id.Version.Major == version.Major &&
                package.Id.Version.Minor == version.Minor &&
                package.Id.Version.Build == version.Patch;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void RunPowerShell(string command)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start package identity registration");
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Package identity registration failed with exit code {process.ExitCode}: {error.Trim()}");
        }
    }

    private static string EscapePowerShellValue(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
