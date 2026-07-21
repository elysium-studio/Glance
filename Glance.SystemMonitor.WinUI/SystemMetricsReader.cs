using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System;

namespace Glance.SystemMonitor.WinUI;

internal sealed class SystemMetricsReader
{
    private ulong previousIdle;
    private ulong previousKernel;
    private ulong previousUser;
    private bool hasPreviousSample;
    private ulong previousBytesReceived;
    private ulong previousBytesSent;
    private long previousNetworkTimestamp;

    public SystemMetrics Read()
    {
        double cpu = ReadCpuUsage();
        MemoryStatus status = new()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatus>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return new SystemMetrics(cpu, 0, 0, 0, 0, 0);
        }

        ulong used = status.TotalPhysical - status.AvailablePhysical;
        double memory = status.TotalPhysical == 0
            ? 0
            : used * 100d / status.TotalPhysical;

        (double download, double upload) = ReadNetworkUsage();

        return new SystemMetrics(cpu, memory, used, status.TotalPhysical, download, upload);
    }

    private (double Download, double Upload) ReadNetworkUsage()
    {
        ulong bytesReceived = 0;
        ulong bytesSent = 0;

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            try
            {
                IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
                bytesReceived += (ulong)Math.Max(0, statistics.BytesReceived);
                bytesSent += (ulong)Math.Max(0, statistics.BytesSent);
            }
            catch (NetworkInformationException)
            {
            }
        }

        long timestamp = Stopwatch.GetTimestamp();

        if (previousNetworkTimestamp == 0)
        {
            previousBytesReceived = bytesReceived;
            previousBytesSent = bytesSent;
            previousNetworkTimestamp = timestamp;
            return (0, 0);
        }

        double elapsed = Stopwatch.GetElapsedTime(previousNetworkTimestamp, timestamp).TotalSeconds;
        double download = elapsed <= 0 || bytesReceived < previousBytesReceived
            ? 0
            : (bytesReceived - previousBytesReceived) / elapsed;
        double upload = elapsed <= 0 || bytesSent < previousBytesSent
            ? 0
            : (bytesSent - previousBytesSent) / elapsed;

        previousBytesReceived = bytesReceived;
        previousBytesSent = bytesSent;
        previousNetworkTimestamp = timestamp;

        return (download, upload);
    }

    private double ReadCpuUsage()
    {
        if (!GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user))
        {
            return 0;
        }

        ulong currentIdle = idle.ToUInt64();
        ulong currentKernel = kernel.ToUInt64();
        ulong currentUser = user.ToUInt64();

        if (!hasPreviousSample)
        {
            previousIdle = currentIdle;
            previousKernel = currentKernel;
            previousUser = currentUser;
            hasPreviousSample = true;
            return 0;
        }

        ulong idleDelta = currentIdle - previousIdle;
        ulong totalDelta = currentKernel - previousKernel + currentUser - previousUser;

        previousIdle = currentIdle;
        previousKernel = currentKernel;
        previousUser = currentUser;

        return totalDelta == 0
            ? 0
            : Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime,
        out FileTime kernelTime,
        out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}

internal readonly record struct SystemMetrics(
    double CpuUsage,
    double MemoryUsage,
    ulong UsedMemory,
    ulong TotalMemory,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond);
