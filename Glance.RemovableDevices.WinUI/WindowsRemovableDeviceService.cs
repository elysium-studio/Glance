using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace Glance.RemovableDevices.WinUI;

public sealed class WindowsRemovableDeviceService :
    IRemovableDeviceService
{
    private const uint ConfigurationManagerSuccess = 0;

    private Dictionary<string, DriveMetadata> cachedMetadata = new(StringComparer.OrdinalIgnoreCase);
    private string driveSignature = string.Empty;

    public IReadOnlyList<RemovableDevice> GetDevices()
    {
        DriveInfo[] drives = DriveInfo.GetDrives();
        string currentSignature = GetDriveSignature(drives);

        if (!string.Equals(currentSignature, driveSignature, StringComparison.Ordinal))
        {
            cachedMetadata = GetDriveMetadata();
            driveSignature = currentSignature;
        }

        List<RemovableDevice> devices = [];

        foreach (DriveInfo drive in drives)
        {
            try
            {
                string rootPath = NormalizeRoot(drive.RootDirectory.FullName);
                cachedMetadata.TryGetValue(rootPath, out DriveMetadata? driveMetadata);

                if (!drive.IsReady || (drive.DriveType != DriveType.Removable && driveMetadata?.IsUsb != true))
                {
                    continue;
                }

                string displayName = !string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.VolumeLabel
                    : driveMetadata?.Model ?? string.Empty;
                string id = driveMetadata?.DeviceInstanceId ?? rootPath;
                bool canEject = !string.IsNullOrWhiteSpace(driveMetadata?.DeviceInstanceId);
                devices.Add(new RemovableDevice(id, rootPath, displayName, drive.TotalSize, drive.AvailableFreeSpace, canEject));
            }
            catch (Exception)
            {
            }
        }

        return devices;
    }

    public bool TryOpen(RemovableDevice device)
    {
        try
        {
            Process.Start(new ProcessStartInfo(device.RootPath)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool TryEject(RemovableDevice device)
    {
        if (!device.CanEject || string.IsNullOrWhiteSpace(device.Id))
        {
            return false;
        }

        if (CM_Locate_DevNodeW(out uint deviceInstance, device.Id, 0) != ConfigurationManagerSuccess)
        {
            return false;
        }

        StringBuilder vetoName = new(260);
        return CM_Request_Device_EjectW(deviceInstance, out _, vetoName, (uint)vetoName.Capacity, 0) == ConfigurationManagerSuccess;
    }

    private static Dictionary<string, DriveMetadata> GetDriveMetadata()
    {
        Dictionary<string, DriveMetadata> metadata = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            using ManagementObjectSearcher searcher = new("SELECT PNPDeviceID, Model, InterfaceType FROM Win32_DiskDrive");
            using ManagementObjectCollection disks = searcher.Get();

            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    string deviceInstanceId = Convert.ToString(disk["PNPDeviceID"]) ?? string.Empty;
                    string model = Convert.ToString(disk["Model"]) ?? string.Empty;
                    bool isUsb = string.Equals(Convert.ToString(disk["InterfaceType"]), "USB", StringComparison.OrdinalIgnoreCase);
                    using ManagementObjectCollection partitions = disk.GetRelated("Win32_DiskPartition");

                    foreach (ManagementObject partition in partitions)
                    {
                        using (partition)
                        {
                            using ManagementObjectCollection logicalDisks = partition.GetRelated("Win32_LogicalDisk");

                            foreach (ManagementObject logicalDisk in logicalDisks)
                            {
                                using (logicalDisk)
                                {
                                    string? deviceId = Convert.ToString(logicalDisk["DeviceID"]);

                                    if (!string.IsNullOrWhiteSpace(deviceId))
                                    {
                                        metadata[NormalizeRoot($"{deviceId}\\")] = new DriveMetadata(deviceInstanceId, model, isUsb);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        return metadata;
    }

    private static string GetDriveSignature(IEnumerable<DriveInfo> drives) =>
        string.Join('|', drives.OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase).Select(GetDriveSignaturePart));

    private static string GetDriveSignaturePart(DriveInfo drive)
    {
        try
        {
            return $"{drive.Name}:{drive.DriveType}:{drive.IsReady}:{(drive.IsReady ? drive.TotalSize : 0)}";
        }
        catch (Exception)
        {
            return $"{drive.Name}:{drive.DriveType}:False:0";
        }
    }

    private static string NormalizeRoot(string rootPath) =>
        Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint deviceInstance, string deviceId, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Request_Device_EjectW(uint deviceInstance, out int vetoType, StringBuilder vetoName, uint vetoNameLength, uint flags);

    private sealed record DriveMetadata(
        string DeviceInstanceId,
        string Model,
        bool IsUsb);
}
