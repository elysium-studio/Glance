using Glance.Application.Abstractions;
using Xunit;

namespace Glance.ScreenLens.Tests;

public sealed class ScreenLensViewModelTests
{
    [Fact]
    public void Extract_RequestsSelectionAndShowsBusyState()
    {
        ScreenLensViewModel viewModel = CreateViewModel();
        bool requested = false;
        viewModel.ExtractionRequested += (_, _) => requested = true;

        viewModel.Extract();

        Assert.True(requested);
        Assert.True(viewModel.IsExtracting);
        Assert.Equal("Select some text", viewModel.StatusText);
    }

    [Fact]
    public void Complete_PublishesRecognizedTextAndLineCount()
    {
        ScreenLensViewModel viewModel = CreateViewModel();

        viewModel.Complete(new ScreenLensResult("Hello\r\nworld", 2, ScreenLensRecognitionEngine.WindowsAi));

        Assert.True(viewModel.HasText);
        Assert.Equal("Hello\r\nworld", viewModel.CompactStatusText);
        Assert.Equal("2 lines detected", viewModel.DetailText);
        Assert.False(viewModel.IsExtracting);
    }

    [Fact]
    public void Copy_OnlyRequestsCopyWhenTextExists()
    {
        ScreenLensViewModel viewModel = CreateViewModel();
        int requests = 0;
        viewModel.CopyRequested += (_, _) => requests++;

        viewModel.Copy();
        viewModel.Complete(new ScreenLensResult("Hello", 1, ScreenLensRecognitionEngine.WindowsOcr));
        viewModel.Copy();

        Assert.Equal(1, requests);
    }

    private static ScreenLensViewModel CreateViewModel() => new(new FakeLocalizer());

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
        {
            ["ModuleTitle"] = "Screen Lens",
            ["ReadyStatus"] = "Ready to extract",
            ["ReadyDetail"] = "Select text anywhere on screen",
            ["SelectingStatus"] = "Select some text",
            ["SelectingDetail"] = "Drag over the area to read",
            ["TextFoundStatus"] = "Text extracted",
            ["LineCountDetail"] = "{0} lines detected",
            ["UnavailableStatus"] = "Text extraction unavailable",
            ["UnavailableDetail"] = "Windows could not read this area"
        };

        public string GetText(string key, params object[] arguments) =>
            string.Format(Values[key], arguments);
    }
}
