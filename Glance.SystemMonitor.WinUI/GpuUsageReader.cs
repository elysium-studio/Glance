using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Glance.SystemMonitor.WinUI;

internal sealed class GpuUsageReader :
    IDisposable
{
    private const uint ErrorSuccess = 0;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhFormatDouble = 0x00000200;
    private nint query;
    private nint counter;

    public GpuUsageReader()
    {
        if (PdhOpenQuery(null, 0, out query) != ErrorSuccess ||
            PdhAddEnglishCounter(query, @"\GPU Engine(*)\Utilization Percentage", 0, out counter) != ErrorSuccess)
        {
            Dispose();
            return;
        }

        _ = PdhCollectQueryData(query);
    }

    public double Read()
    {
        if (query == 0 || counter == 0 || PdhCollectQueryData(query) != ErrorSuccess)
        {
            return 0;
        }

        uint bufferSize = 0;
        uint itemCount = 0;

        if (PdhGetFormattedCounterArray(counter, PdhFormatDouble, ref bufferSize, ref itemCount, 0) != PdhMoreData ||
            bufferSize == 0)
        {
            return 0;
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)bufferSize));

        try
        {
            if (PdhGetFormattedCounterArray(counter, PdhFormatDouble, ref bufferSize, ref itemCount, buffer) != ErrorSuccess)
            {
                return 0;
            }

            Dictionary<string, double> engineUsage = new(StringComparer.OrdinalIgnoreCase);
            int itemSize = Marshal.SizeOf<PdhFormattedCounterValueItem>();

            for (int index = 0; index < itemCount; index++)
            {
                PdhFormattedCounterValueItem item = Marshal.PtrToStructure<PdhFormattedCounterValueItem>(buffer + (index * itemSize));

                if (item.Status != ErrorSuccess || !double.IsFinite(item.Value) || item.Value <= 0)
                {
                    continue;
                }

                string? instanceName = Marshal.PtrToStringUni(item.Name);
                string? engineId = GetEngineId(instanceName);

                if (engineId is not null)
                {
                    engineUsage[engineId] = engineUsage.GetValueOrDefault(engineId) + item.Value;
                }
            }

            return engineUsage.Count == 0
                ? 0
                : Math.Clamp(engineUsage.Values.Max(), 0, 100);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (query != 0)
        {
            _ = PdhCloseQuery(query);
            query = 0;
            counter = 0;
        }
    }

    private static string? GetEngineId(string? instanceName)
    {
        int luidIndex = instanceName?.IndexOf("_luid_", StringComparison.OrdinalIgnoreCase) ?? -1;
        return luidIndex < 0 ? null : instanceName![luidIndex..];
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PdhFormattedCounterValueItem
    {
        public readonly nint Name;
        public readonly uint Status;
        public readonly double Value;
    }

    [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource,
        nuint userData,
        out nint query);

    [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(nint query,
        string counterPath,
        nuint userData,
        out nint counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(nint query);

    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    private static extern uint PdhGetFormattedCounterArray(nint counter,
        uint format,
        ref uint bufferSize,
        ref uint itemCount,
        nint itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(nint query);
}
