using Microsoft.Win32;
using System;

namespace Glance.PrivacyControls.WinUI;

internal static class WindowsCapabilityUsageReader
{
    private const string ConsentStorePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public static bool IsInUse(string capability) =>
        IsInUse(RegistryHive.CurrentUser, capability) ||
        IsInUse(RegistryHive.LocalMachine, capability);

    private static bool IsInUse(RegistryHive hive, string capability)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using RegistryKey? capabilityKey = baseKey.OpenSubKey($@"{ConsentStorePath}\{capability}");

            if (capabilityKey is null)
            {
                return false;
            }

            foreach (string applicationName in capabilityKey.GetSubKeyNames())
            {
                using RegistryKey? applicationKey = capabilityKey.OpenSubKey(applicationName);

                if (applicationKey is null)
                {
                    continue;
                }

                if (applicationName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                {
                    if (AnyChildIsInUse(applicationKey))
                    {
                        return true;
                    }
                }
                else if (IsKeyInUse(applicationKey))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Missing or inaccessible capability records mean no observable usage.
        }

        return false;
    }

    private static bool AnyChildIsInUse(RegistryKey parentKey)
    {
        foreach (string childName in parentKey.GetSubKeyNames())
        {
            using RegistryKey? childKey = parentKey.OpenSubKey(childName);

            if (childKey is not null && IsKeyInUse(childKey))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKeyInUse(RegistryKey key) =>
        key.GetValue("LastUsedTimeStop") switch
        {
            long value => value == 0,
            int value => value == 0,
            _ => false
        };
}
