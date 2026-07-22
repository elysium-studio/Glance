using Glance.Shell;
using System.Text.Json;
using Xunit;

namespace Glance.Tests;

public sealed class GlanceSettingsTests
{
    [Fact]
    public void DefaultsMatchDesktopExperience()
    {
        GlanceSettings settings = new();

        Assert.False(settings.AutoHide);
        Assert.Empty(settings.Modules);
        Assert.Equal(GlancePlacement.Top, settings.Placement);
        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void SourceGeneratedJsonPreservesSettings()
    {
        GlanceSettings settings = new()
        {
            AutoHide = true,
            Placement = GlancePlacement.Bottom,
            StartWithWindows = false,
            Modules =
            [
                new GlanceModulePreference
                {
                    Id = "Timer",
                    IsEnabled = false
                }
            ]
        };

        string json = JsonSerializer.Serialize(settings, GlanceJsonContext.Default.GlanceSettings);
        GlanceSettings? result = JsonSerializer.Deserialize(json, GlanceJsonContext.Default.GlanceSettings);

        Assert.NotNull(result);
        Assert.True(result.AutoHide);
        Assert.Equal(GlancePlacement.Bottom, result.Placement);
        Assert.False(result.StartWithWindows);
        Assert.Single(result.Modules);
        Assert.Equal("Timer", result.Modules[0].Id);
        Assert.False(result.Modules[0].IsEnabled);
    }
}
