using Glance.Application.Abstractions;

namespace Glance.ScreenCapture.Tests;

public sealed class ScreenCaptureViewModelTests
{
    [Theory]
    [InlineData(ScreenCaptureMode.Region)]
    [InlineData(ScreenCaptureMode.Window)]
    [InlineData(ScreenCaptureMode.Display)]
    [InlineData(ScreenCaptureMode.AllDisplays)]
    public void CaptureFunctions_RequestExpectedMode(ScreenCaptureMode expectedMode)
    {
        ScreenCaptureViewModel viewModel = CreateViewModel();
        ScreenCaptureMode? requestedMode = null;
        viewModel.CaptureRequested += (_, mode) => requestedMode = mode;

        InvokeCapture(viewModel, expectedMode);

        Assert.True(viewModel.IsCapturing);
        Assert.Equal(expectedMode, requestedMode);
    }

    [Fact]
    public void CompleteCapture_AddsAndSelectsMostRecentCapture()
    {
        ScreenCaptureViewModel viewModel = CreateViewModel();
        ScreenCaptureItem first = CreateCapture("first.png", 800, 600);
        ScreenCaptureItem second = CreateCapture("second.png", 1920, 1080);

        viewModel.SetCaptures([first]);
        viewModel.CompleteCapture(second);

        Assert.True(viewModel.HasCaptures);
        Assert.Equal(2, viewModel.Captures.Count);
        Assert.Equal(second, viewModel.SelectedCapture?.Capture);
        Assert.Equal("1920 × 1080", viewModel.SelectedCapture?.Detail);
    }

    [Fact]
    public void CompleteCapture_KeepsOnlySixRecentCaptures()
    {
        ScreenCaptureViewModel viewModel = CreateViewModel();

        for (int index = 0; index < 8; index++)
        {
            viewModel.CompleteCapture(CreateCapture($"capture-{index}.png", 100 + index, 100));
        }

        Assert.Equal(6, viewModel.Captures.Count);
        Assert.Equal("capture-7.png", viewModel.Captures[0].FileName);
        Assert.Equal("capture-2.png", viewModel.Captures[^1].FileName);
    }

    [Fact]
    public void RecentCaptureLimit_ComesFromModuleSettings()
    {
        ScreenCaptureViewModel viewModel = new(new FakeLocalizer(), new ScreenCaptureSettings { RecentCaptureLimit = 2 });

        for (int index = 0; index < 4; index++)
        {
            viewModel.CompleteCapture(CreateCapture($"capture-{index}.png", 100, 100));
        }

        Assert.Equal(2, viewModel.Captures.Count);
    }

    [Fact]
    public void Remove_LastCapture_RestoresEmptyState()
    {
        ScreenCaptureViewModel viewModel = CreateViewModel();
        ScreenCaptureItem capture = CreateCapture("capture.png", 640, 480);
        viewModel.SetCaptures([capture]);

        viewModel.Remove(capture);

        Assert.False(viewModel.HasCaptures);
        Assert.Null(viewModel.SelectedCapture);
        Assert.Equal("Ready to capture", viewModel.StatusText);
    }

    [Fact]
    public void CaptureModeLabels_AreProvidedByLocalizer()
    {
        ScreenCaptureViewModel viewModel = CreateViewModel();

        Assert.Equal("Region", viewModel.CaptureRegionText);
        Assert.Equal("Window", viewModel.CaptureWindowText);
        Assert.Equal("Display", viewModel.CaptureDisplayText);
        Assert.Equal("All displays", viewModel.CaptureAllDisplaysText);
    }

    private static ScreenCaptureViewModel CreateViewModel() => new(new FakeLocalizer());

    private static ScreenCaptureItem CreateCapture(string fileName, int width, int height) =>
        new(Path.Combine("C:\\Captures", fileName), fileName, DateTimeOffset.Now, width, height, ScreenCaptureMode.Region);

    private static void InvokeCapture(ScreenCaptureViewModel viewModel, ScreenCaptureMode mode)
    {
        switch (mode)
        {
            case ScreenCaptureMode.Region:
                viewModel.CaptureRegion();
                break;
            case ScreenCaptureMode.Window:
                viewModel.CaptureWindow();
                break;
            case ScreenCaptureMode.Display:
                viewModel.CaptureDisplay();
                break;
            case ScreenCaptureMode.AllDisplays:
                viewModel.CaptureAllDisplays();
                break;
        }
    }

    private sealed class FakeLocalizer : ITextLocalizer
    {
        private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
        {
            ["ModuleTitle"] = "Screen capture",
            ["ReadyToCapture"] = "Ready to capture",
            ["SelectingCapture"] = "Select what to capture",
            ["CaptureSaved"] = "Capture saved",
            ["CaptureFailed"] = "Capture failed",
            ["CaptureDetail"] = "{0} × {1}",
            ["CaptureRegion"] = "Region",
            ["CaptureWindow"] = "Window",
            ["CaptureDisplay"] = "Display",
            ["CaptureAllDisplays"] = "All displays"
        };

        public string GetText(string key, params object[] arguments) =>
            string.Format(Values[key], arguments);
    }
}
