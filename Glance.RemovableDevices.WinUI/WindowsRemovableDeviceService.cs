using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Glance.RemovableDevices.WinUI;

public sealed class WindowsRemovableDeviceService :
    IRemovableDeviceService
{
    private const uint ConfigurationManagerSuccess = 0;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint StorageDeviceProperty = 0;
    private const uint StoragePropertyStandardQuery = 0;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int StorageBusTypeUsb = 7;
    private const int StorageDeviceDescriptorMinimumSize = 36;
    private const int StorageDeviceDescriptorBufferSize = 1024;

    private Dictionary<string, DriveMetadata> cachedMetadata = [with(StringComparer.OrdinalIgnoreCase)];
    private string driveSignature = string.Empty;

    public IReadOnlyList<RemovableDevice> GetDevices()
    {
        DriveInfo[] drives = DriveInfo.GetDrives();
        string currentSignature = GetDriveSignature(drives);

        if (!string.Equals(currentSignature, driveSignature, StringComparison.Ordinal))
        {
            cachedMetadata = GetDriveMetadata(drives);
            driveSignature = currentSignature;
        }

        List<RemovableDevice> devices = [];

        foreach (DriveInfo drive in drives)
        {
            try
            {
                string rootPath = NormalizeRoot(drive.RootDirectory.FullName);
                _ = cachedMetadata.TryGetValue(rootPath, out DriveMetadata? driveMetadata);

                if (!drive.IsReady || (drive.DriveType != DriveType.Removable && driveMetadata?.IsUsb != true))
                {
                    continue;
                }

                string displayName = !string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.VolumeLabel
                    : driveMetadata?.Model ?? string.Empty;
                string id = !string.IsNullOrWhiteSpace(driveMetadata?.DeviceInstanceId)
                    ? driveMetadata.DeviceInstanceId
                    : rootPath;
                devices.Add(new RemovableDevice(id, rootPath, displayName, drive.TotalSize, drive.AvailableFreeSpace, true));
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
            _ = Process.Start(new ProcessStartInfo(device.RootPath)
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
        if (!device.CanEject)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(device.Id) && CM_Locate_DevNodeW(out uint deviceInstance, device.Id, 0) == ConfigurationManagerSuccess)
        {
            StringBuilder vetoName = new(260);

            if (CM_Request_Device_EjectW(deviceInstance, out _, vetoName, (uint)vetoName.Capacity, 0) == ConfigurationManagerSuccess)
            {
                return true;
            }
        }

        return TryShellEject(device.RootPath);
    }

    private static Dictionary<string, DriveMetadata> GetDriveMetadata(IEnumerable<DriveInfo> drives)
    {
        Dictionary<string, DriveMetadata> metadata = [with(StringComparer.OrdinalIgnoreCase)];

        foreach (DriveInfo drive in drives)
        {
            try
            {
                string rootPath = NormalizeRoot(drive.RootDirectory.FullName);

                if (TryGetDriveMetadata(rootPath, out DriveMetadata? driveMetadata) && driveMetadata is not null)
                {
                    metadata[rootPath] = driveMetadata;
                }
            }
            catch (Exception)
            {
            }
        }

        return metadata;
    }

    private static bool TryGetDriveMetadata(string rootPath, out DriveMetadata? metadata)
    {
        metadata = null;
        string volumePath = $@"\\.\{rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}";
        using SafeFileHandle handle = CreateFileW(volumePath, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return false;
        }

        byte[] query = new byte[12];
        BitConverter.GetBytes(StorageDeviceProperty).CopyTo(query, 0);
        BitConverter.GetBytes(StoragePropertyStandardQuery).CopyTo(query, 4);
        byte[] descriptor = new byte[StorageDeviceDescriptorBufferSize];

        if (!DeviceIoControl(handle, IoctlStorageQueryProperty, query, (uint)query.Length, descriptor, (uint)descriptor.Length, out uint bytesReturned, IntPtr.Zero) ||
            bytesReturned < StorageDeviceDescriptorMinimumSize)
        {
            return false;
        }

        string vendor = ReadDescriptorString(descriptor, bytesReturned, BitConverter.ToUInt32(descriptor, 12));
        string product = ReadDescriptorString(descriptor, bytesReturned, BitConverter.ToUInt32(descriptor, 16));
        string model = string.Join(' ', new[] { vendor, product }.Where(value => !string.IsNullOrWhiteSpace(value)));
        int busType = BitConverter.ToInt32(descriptor, 28);
        metadata = new DriveMetadata(string.Empty, model, busType == StorageBusTypeUsb);
        return true;
    }

    private static string ReadDescriptorString(byte[] descriptor, uint bytesReturned, uint offset)
    {
        if (offset == 0 || offset >= bytesReturned || offset >= descriptor.Length)
        {
            return string.Empty;
        }

        int start = (int)offset;
        int limit = Math.Min((int)bytesReturned, descriptor.Length);
        int end = start;

        while (end < limit && descriptor[end] != 0)
        {
            end++;
        }

        return Encoding.ASCII.GetString(descriptor, start, end - start).Trim();
    }

    private static string GetDriveSignature(IEnumerable<DriveInfo> drives) => string.Join('|', drives.OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase).Select(GetDriveSignaturePart));

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

    private static bool TryShellEject(string rootPath)
    {
        bool ejected = false;
        Thread thread = new(() => ejected = TryShellEjectCore(rootPath));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return ejected;
    }

    private static bool TryShellEjectCore(string rootPath)
    {
        object? shell = null;
        object? drives = null;
        object? item = null;

        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");

            if (shellType is null)
            {
                return false;
            }

            shell = Activator.CreateInstance(shellType);
            drives = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, [17]);
            item = drives?.GetType().InvokeMember("ParseName", BindingFlags.InvokeMethod, null, drives, [rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)]);

            if (item is null)
            {
                return false;
            }

            _ = item.GetType().InvokeMember("InvokeVerb", BindingFlags.InvokeMethod, null, item, ["Eject"]);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(item);
            ReleaseComObject(drives);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }

    private static string NormalizeRoot(string rootPath) => Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint deviceInstance, string deviceId, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Request_Device_EjectW(uint deviceInstance, out int vetoType, StringBuilder vetoName, uint vetoNameLength, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, byte[] inputBuffer, uint inputBufferSize, byte[] outputBuffer, uint outputBufferSize, out uint bytesReturned, IntPtr overlapped);

    private sealed record DriveMetadata(string DeviceInstanceId,
        string Model,
        bool IsUsb);
}
