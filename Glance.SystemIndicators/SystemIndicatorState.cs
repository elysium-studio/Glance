namespace Glance.SystemIndicators;

public sealed record SystemIndicatorState(SystemIndicatorKind Kind,
    int? Level = null,
    bool? IsEnabled = null)
{
    public int? NormalizedLevel => Level is null ? null : Math.Clamp(Level.Value, 0, 100);
}
