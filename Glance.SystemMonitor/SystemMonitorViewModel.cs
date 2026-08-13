using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.SystemMonitor;

public sealed partial class SystemMonitorViewModel(ITextLocalizer localizer) :
    ObservableObject
{
    private readonly ITextLocalizer localizer = localizer;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double memoryUsage;

    [ObservableProperty]
    private string cpuText = "0%";

    [ObservableProperty]
    private string memoryText = "0%";

    [ObservableProperty]
    private string memoryDetail = localizer.GetText("CalculatingMemory");

    [ObservableProperty]
    private double gpuUsage;

    [ObservableProperty]
    private string gpuText = "0%";

    public event EventHandler? MetricsUpdated;

    public void Update(double cpu,
        double memory,
        ulong usedBytes,
        ulong totalBytes,
        double gpu)
    {
        CpuUsage = cpu;
        MemoryUsage = memory;
        GpuUsage = gpu;
        CpuText = $"{cpu:0}%";
        MemoryText = $"{memory:0}%";
        GpuText = $"{gpu:0}%";
        MemoryDetail = localizer.GetText("MemoryUsageFormat", FormatBytes(usedBytes), FormatBytes(totalBytes));
        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:0.0} GB";
    }

}
