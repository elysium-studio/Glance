using Glance.SystemIndicators;

namespace Glance.SystemIndicators.Tests;

public sealed class SystemIndicatorsViewModelTests
{
    [Fact]
    public void UpdateCreatesCompactSummaryAndLevel()
    {
        SystemIndicatorsViewModel viewModel = new();

        viewModel.Update(new SystemIndicatorPresentation("Volume",
            "42%",
            "System sound level",
            "glyph",
            42));

        Assert.Equal("Volume \u00B7 42%", viewModel.CompactText);
        Assert.Equal(42, viewModel.Level);
        Assert.True(viewModel.IsLevelVisible);
    }

    [Fact]
    public void UpdateHidesLevelForToggleIndicators()
    {
        SystemIndicatorsViewModel viewModel = new();

        viewModel.Update(new SystemIndicatorPresentation("Caps Lock",
            "On",
            "Letters will be uppercase",
            "glyph"));

        Assert.False(viewModel.IsLevelVisible);
        Assert.Equal(0, viewModel.Level);
    }
}
