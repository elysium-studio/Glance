using CommunityToolkit.Mvvm.ComponentModel;
using Glance.Application.Abstractions;

namespace Glance.SystemMonitor;

public partial class SystemMonitorViewModel : ObservableObject
{
    private readonly ITextLocalizer localizer;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double memoryUsage;

    [ObservableProperty]
    private string cpuText = "0%";

    [ObservableProperty]
    private string memoryText = "0%";

    [ObservableProperty]
    private string memoryDetail;

    [ObservableProperty]
    private string downloadText = "0 KB/s";

    [ObservableProperty]
    private string uploadText = "0 KB/s";

    public SystemMonitorViewModel(ITextLocalizer localizer)
    {
        this.localizer = localizer;
        memoryDetail = localizer.GetText("CalculatingMemory");
    }

    public void Update(
        double cpu,
        double memory,
        ulong usedBytes,
        ulong totalBytes,
        double downloadBytesPerSecond,
        double uploadBytesPerSecond)
    {
        CpuUsage = cpu;
        MemoryUsage = memory;
        CpuText = $"{cpu:0}%";
        MemoryText = $"{memory:0}%";
        MemoryDetail = localizer.GetText("MemoryUsageFormat", FormatBytes(usedBytes), FormatBytes(totalBytes));
        DownloadText = FormatRate(downloadBytesPerSecond);
        UploadText = FormatRate(uploadBytesPerSecond);
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:0.0} GB";
    }

    private static string FormatRate(double bytesPerSecond) => bytesPerSecond switch
    {
        >= 1024d * 1024d => $"{bytesPerSecond / (1024d * 1024d):0.0} MB/s",
        >= 1024d => $"{bytesPerSecond / 1024d:0} KB/s",
        _ => $"{bytesPerSecond:0} B/s"
    };
}
