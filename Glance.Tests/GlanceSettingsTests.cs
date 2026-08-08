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
        Assert.Equal(GlanceExpansionMode.ExpandOnHover, settings.ExpansionMode);
        Assert.Empty(settings.Modules);
        Assert.Equal(GlancePlacement.Top, settings.Placement);
        Assert.True(settings.ShowSetupOnStartup);
        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void SourceGeneratedJsonPreservesSettings()
    {
        GlanceSettings settings = new()
        {
            AutoHide = true,
            ExpansionMode = GlanceExpansionMode.AlwaysExpanded,
            Placement = GlancePlacement.Bottom,
            ShowSetupOnStartup = false,
            StartWithWindows = false,
            Modules =
            [
                new GlanceModulePreference
                {
                    Id = "Timer",
                    IsAttentionEnabled = false,
                    IsEnabled = false
                }
            ]
        };

        string json = JsonSerializer.Serialize(settings, GlanceJsonContext.Default.GlanceSettings);
        GlanceSettings? result = JsonSerializer.Deserialize(json, GlanceJsonContext.Default.GlanceSettings);

        Assert.NotNull(result);
        Assert.True(result.AutoHide);
        Assert.Equal(GlanceExpansionMode.AlwaysExpanded, result.ExpansionMode);
        Assert.Equal(GlancePlacement.Bottom, result.Placement);
        Assert.False(result.ShowSetupOnStartup);
        Assert.False(result.StartWithWindows);
        _ = Assert.Single(result.Modules);
        Assert.Equal("Timer", result.Modules[0].Id);
        Assert.False(result.Modules[0].IsAttentionEnabled);
        Assert.False(result.Modules[0].IsEnabled);
    }
}
