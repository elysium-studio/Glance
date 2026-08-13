using System;
using System.Runtime.InteropServices;

namespace Glance.SystemMonitor.WinUI;

internal sealed class SystemMetricsReader :
    IDisposable
{
    private readonly GpuUsageReader gpuUsageReader = new();
    private ulong previousIdle;
    private ulong previousKernel;
    private ulong previousUser;
    private bool hasPreviousSample;

    public SystemMetrics Read()
    {
        double cpu = ReadCpuUsage();
        MemoryStatus status = new()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatus>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return new SystemMetrics(cpu, 0, 0, 0, gpuUsageReader.Read());
        }

        ulong used = status.TotalPhysical - status.AvailablePhysical;
        double memory = status.TotalPhysical == 0
            ? 0
            : used * 100d / status.TotalPhysical;

        return new SystemMetrics(cpu, memory, used, status.TotalPhysical, gpuUsageReader.Read());
    }

    public void Dispose() => gpuUsageReader.Dispose();

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
    private static extern bool GetSystemTimes(out FileTime idleTime,
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

internal readonly record struct SystemMetrics(double CpuUsage,
    double MemoryUsage,
    ulong UsedMemory,
    ulong TotalMemory,
    double GpuUsage);
