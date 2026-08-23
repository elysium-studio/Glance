namespace Glance.Shell;

public sealed record GlanceModuleFeedStatus(GlanceModuleFeedSource Source, bool IsAvailable, bool IsUsingCache, string? ErrorMessage);
