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
        Assert.Empty(settings.Converters);
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
            ExpansionMode = GlanceExpansionMode.ExpandOnClick,
            Placement = GlancePlacement.Bottom,
            TranscriptionModelId = "nemotron-3.5-asr-streaming-0.6b",
            ShowSetupOnStartup = false,
            StartWithWindows = false,
            Converters =
            [
                new GlanceQuickConverterPreference
                {
                    Id = "QuickConvert.Video",
                    IsEnabled = false
                }
            ],
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
        Assert.Equal(GlanceExpansionMode.ExpandOnClick, result.ExpansionMode);
        Assert.Equal(GlancePlacement.Bottom, result.Placement);
        Assert.Equal("nemotron-3.5-asr-streaming-0.6b", result.TranscriptionModelId);
        Assert.False(result.ShowSetupOnStartup);
        Assert.False(result.StartWithWindows);
        _ = Assert.Single(result.Converters);
        Assert.Equal("QuickConvert.Video", result.Converters[0].Id);
        Assert.False(result.Converters[0].IsEnabled);
        _ = Assert.Single(result.Modules);
        Assert.Equal("Timer", result.Modules[0].Id);
        Assert.False(result.Modules[0].IsAttentionEnabled);
        Assert.False(result.Modules[0].IsEnabled);
    }
}
