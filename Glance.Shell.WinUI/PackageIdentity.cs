using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace Glance.Shell.WinUI;

internal static partial class PackageIdentity
{
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;

    public static bool IsPackaged
    {
        get
        {
            uint packageFullNameLength = 0;
            int result = GetCurrentPackageFullName(ref packageFullNameLength, 0);

            return result switch
            {
                ErrorInsufficientBuffer => true,
                AppModelErrorNoPackage => false,
                _ => throw new Win32Exception(result)
            };
        }
    }

    public static bool IsExternalLocation => IsPackaged && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !string.IsNullOrWhiteSpace(Package.Current.EffectiveExternalPath);

    public static bool IsFullPackage => IsPackaged && !IsExternalLocation;

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref uint packageFullNameLength, nint packageFullName);
}
