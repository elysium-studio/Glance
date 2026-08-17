namespace Glance.Application.Abstractions;

public sealed record GlanceQuickConversionProgress(GlanceQuickConversionStage Stage,
    double Progress,
    bool IsComplete = false);

public enum GlanceQuickConversionStage
{
    Setup,
    Conversion
}
