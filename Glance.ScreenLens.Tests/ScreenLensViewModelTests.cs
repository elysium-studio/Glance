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
        Assert.Equal("Extract text", viewModel.StatusText);
    }

    [Fact]
    public void Complete_RestoresLauncherState()
    {
        ScreenLensViewModel viewModel = CreateViewModel();
        viewModel.Extract();

        viewModel.Complete();

        Assert.False(viewModel.IsExtracting);
        Assert.Equal("Extract text", viewModel.StatusText);
    }

    private static ScreenLensViewModel CreateViewModel() => new(new FakeLocalizer());

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        private static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
        {
            ["ModuleTitle"] = "Screen Lens",
            ["ReadyStatus"] = "Extract text"
        };

        public string GetText(string key, params object[] arguments) => string.Format(Values[key], arguments);
    }
}
