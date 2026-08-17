using Glance.Application.Abstractions;

namespace Glance.QuickConvert.Tests;

public sealed class QuickConvertViewModelTests
{
    [Fact]
    public void ShowsFirstUseSetupProgress()
    {
        QuickConvertViewModel viewModel = new(new FakeLocalizer());
        viewModel.BeginConversion(1);

        viewModel.ShowToolSetup(0.42);

        Assert.True(viewModel.IsBusy);
        Assert.Equal("SettingUp", viewModel.Summary);
        Assert.Equal("DownloadingTools:42", viewModel.Detail);
    }

    [Fact]
    public void LeavesBusyStateWhenSetupFails()
    {
        QuickConvertViewModel viewModel = new(new FakeLocalizer());
        viewModel.BeginConversion(1);
        viewModel.ShowToolSetup(0.42);

        viewModel.ShowToolSetupFailure();

        Assert.False(viewModel.IsBusy);
        Assert.Equal("SetupFailed", viewModel.Summary);
        Assert.Equal("SetupFailedDetail", viewModel.Detail);
    }

    private sealed class FakeLocalizer :
        ITextLocalizer
    {
        public string GetText(string key,
            params object[] arguments) => arguments.Length == 0
                ? key
                : $"{key}:{string.Join(',', arguments)}";
    }
}
