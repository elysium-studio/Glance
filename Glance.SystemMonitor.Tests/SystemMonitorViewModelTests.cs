using Glance.Application.Abstractions;

namespace Glance.SystemMonitor.Tests;

public sealed class SystemMonitorViewModelTests
{
    [Fact]
    public void Constructor_UsesLocalizedMemoryPlaceholder()
    {
        SystemMonitorViewModel viewModel = new(new TestTextLocalizer());

        Assert.Equal("CalculatingMemory", viewModel.MemoryDetail);
        Assert.Equal("0%", viewModel.CpuText);
        Assert.Equal("0%", viewModel.MemoryText);
        Assert.Equal("0 KB/s", viewModel.DownloadText);
        Assert.Equal("0 KB/s", viewModel.UploadText);
    }

    [Fact]
    public void Update_SetsUsageValuesAndRoundedLabels()
    {
        SystemMonitorViewModel viewModel = CreateViewModel();

        viewModel.Update(42.4, 67.8, 0, 0, 0, 0);

        Assert.Equal(42.4, viewModel.CpuUsage);
        Assert.Equal(67.8, viewModel.MemoryUsage);
        Assert.Equal("42%", viewModel.CpuText);
        Assert.Equal("68%", viewModel.MemoryText);
    }

    [Fact]
    public void Update_FormatsMemoryInGigabytes()
    {
        SystemMonitorViewModel viewModel = CreateViewModel();
        const ulong gigabyte = 1024UL * 1024UL * 1024UL;

        viewModel.Update(0, 0, 6 * gigabyte, 16 * gigabyte, 0, 0);

        Assert.Equal("MemoryUsageFormat(6.0 GB,16.0 GB)", viewModel.MemoryDetail);
    }

    [Theory]
    [InlineData(512, "512 B/s")]
    [InlineData(1024, "1 KB/s")]
    [InlineData(1536, "2 KB/s")]
    [InlineData(1048576, "1.0 MB/s")]
    [InlineData(2621440, "2.5 MB/s")]
    public void Update_FormatsDownloadRate(double bytesPerSecond, string expected)
    {
        SystemMonitorViewModel viewModel = CreateViewModel();

        viewModel.Update(0, 0, 0, 0, bytesPerSecond, 0);

        Assert.Equal(expected, viewModel.DownloadText);
    }

    [Theory]
    [InlineData(128, "128 B/s")]
    [InlineData(10240, "10 KB/s")]
    [InlineData(1572864, "1.5 MB/s")]
    public void Update_FormatsUploadRate(double bytesPerSecond, string expected)
    {
        SystemMonitorViewModel viewModel = CreateViewModel();

        viewModel.Update(0, 0, 0, 0, 0, bytesPerSecond);

        Assert.Equal(expected, viewModel.UploadText);
    }

    private static SystemMonitorViewModel CreateViewModel() => new(new TestTextLocalizer());

    private sealed class TestTextLocalizer : ITextLocalizer
    {
        public string GetText(string key, params object[] arguments) =>
            arguments.Length == 0 ? key : $"{key}({string.Join(',', arguments)})";
    }
}
