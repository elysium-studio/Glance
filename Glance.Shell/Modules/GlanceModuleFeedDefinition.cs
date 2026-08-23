namespace Glance.Shell;

public sealed record GlanceModuleFeedDefinition(string Id, string DisplayName, Uri Uri, bool IsEnabled, bool AllowLocalPackages, int Priority);
