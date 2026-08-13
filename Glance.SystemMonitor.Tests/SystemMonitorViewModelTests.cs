using Glance.Application.Abstractions;

namespace Glance.SystemMonitor.Tests;

public sealed class SystemMonitorViewModelTests
{
    [Fact]
    public void Settings_ShowPerformanceChartsByDefault()
    {
        SystemMonitorSettings settings = new();

        Assert.True(settings.ShowPerformanceCharts);
    }

    [Fact]
    public void Constructor_UsesLocalizedMemoryPlaceholder()
    {
        SystemMonitorViewModel viewModel = new(new TestTextLocalizer());

        Assert.Equal("CalculatingMemory", viewModel.MemoryDetail);
        Assert.Equal("0%", viewModel.CpuText);
        Assert.Equal("0%", viewModel.MemoryText);
        Assert.Equal("0%", viewModel.GpuText);
    }

    [Fact]
    public void Update_SetsUsageValuesAndRoundedLabels()
    {
        SystemMonitorViewModel viewModel = CreateViewModel();

        viewModel.Update(42.4, 67.8, 0, 0, 26.2);

        Assert.Equal(42.4, viewModel.CpuUsage);
        Assert.Equal(67.8, viewModel.MemoryUsage);
        Assert.Equal(26.2, viewModel.GpuUsage);
        Assert.Equal("42%", viewModel.CpuText);
        Assert.Equal("68%", viewModel.MemoryText);
        Assert.Equal("26%", viewModel.GpuText);
    }

    [Fact]
    public void Update_RaisesOneMetricsUpdatedEvent()
    {
        SystemMonitorViewModel viewModel = CreateViewModel();
        int updateCount = 0;
        viewModel.MetricsUpdated += (_, _) => updateCount++;

        viewModel.Update(42.4, 67.8, 0, 0, 25);

        Assert.Equal(1, updateCount);
    }

    [Fact]
    public void Update_FormatsMemoryInGigabytes()
    {
        SystemMonitorViewModel viewModel = CreateViewModel();
        const ulong gigabyte = 1024UL * 1024UL * 1024UL;

        viewModel.Update(0, 0, 6 * gigabyte, 16 * gigabyte, 0);

        Assert.Equal("MemoryUsageFormat(6.0 GB,16.0 GB)", viewModel.MemoryDetail);
    }

    private static SystemMonitorViewModel CreateViewModel() => new(new TestTextLocalizer());

    private sealed class TestTextLocalizer :
        ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) => arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
